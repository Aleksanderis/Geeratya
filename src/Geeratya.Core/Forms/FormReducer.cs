using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Transitions.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The form part of the reducer (<c>docs/architecture.md</c>, "Where form state lives"): opens a form on a tab, fills
/// it from the metadata reply, edits the focused field, checks the form locally on submit and hands the changed
/// fields to the write pipeline. The form is open while the mode is Insert and the active tab holds one. Pure.
/// </summary>
public static class FormReducer
{
    private const string NoIssueText = "No issue selected: select a row or open an issue first";
    private const string NoFieldsText = "Nothing here can be edited";
    private const string NothingChangedText = "Nothing changed";
    private const string DetachedText = "Closed the form; the save continues";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>True when the keys go to a form: Insert mode, no picker or profile form, the tab has one.</summary>
    public static bool IsOpen(AppState state)
    {
        return OpenForm(state) is not null;
    }

    /// <summary>The form that takes the keys, or null.</summary>
    public static FormState? OpenForm(AppState state)
    {
        if (state.Mode != InputMode.Insert || state.Picker is not null || state.Form is not null)
        {
            return null;
        }

        return ActiveTab(state)?.Form;
    }

    public static ReduceResult Reduce(AppState state, FormCommand command)
    {
        return command switch
        {
            OpenFormRequested { Target: AssignFormTarget assign } open => OpenAssign(state, open.Tab, assign),
            OpenFormRequested open => Open(state, open.Tab, open.Target),
            OpenLabelsFormRequested => OpenLabels(state),
            FormMetadataLoaded loaded => Loaded(state, loaded),
            FieldOptionsLoaded found => Found(state, found),
            FieldOptionsFailed failed => SearchFailed(state, failed),
            FormMetadataFailed failed => Failed(state, failed),
            _ => new ReduceResult(state, _noEffects),
        };
    }

    /// <summary>A key while a form is open. Printable keys are text; only Esc, Enter and Ctrl- keys act.</summary>
    public static ReduceResult Key(AppState state, string key, DateTimeOffset at, KeymapTable keymap)
    {
        var tab = ActiveTab(state)!;
        var form = tab.Form!;
        var action = keymap.Resolve(InputMode.Insert, string.Empty, key).Action;

        if (form.IsSaving)
        {
            return Saving(state, tab, action);
        }

        if (form.DiscardPending)
        {
            return Discarding(state, tab, keymap.Resolve(InputMode.Confirm, string.Empty, key).Action, action);
        }

        return action switch
        {
            ActionIds.Cancel => Cancel(state, tab, form),
            ActionIds.FormSubmit => Submit(state, tab, form, at),
            ActionIds.FormFieldNext => Result(Replace(state, tab, form with { Focus = Step(form, 1) })),
            ActionIds.FormFieldPrevious => Result(Replace(state, tab, form with { Focus = Step(form, -1) })),
            ActionIds.FormNext => Enter(state, tab, form, at),
            ActionIds.FormBackspace => Searched(state, Edit(state, tab, form, FormEditor.Backspace)),
            ActionIds.PickerNext => Result(Edit(state, tab, form, field => FormEditor.Move(field, 1))),
            ActionIds.PickerPrevious => Result(Edit(state, tab, form, field => FormEditor.Move(field, -1))),
            null => Searched(state, Typed(state, tab, form, KeyNotation.ToText(key))),
            _ => Result(state),
        };
    }

    /// <summary>Pasted text goes to the focused field (first line only).</summary>
    public static ReduceResult Paste(AppState state, string text)
    {
        var tab = ActiveTab(state)!;
        var form = tab.Form!;

        if (form.IsSaving || form.DiscardPending)
        {
            return Result(state);
        }

        return Searched(state, Typed(state, tab, form, text));
    }

    /// <summary>
    /// Called after a write command was reduced: when the operation behind a form's save has ended, the form closes
    /// on success, or unlocks with the errors mapped to its fields on failure. A write that is still running
    /// (verifying a lost reply) changes nothing.
    /// </summary>
    public static ReduceResult Settled(ReduceResult result, long operationId, bool applied, WriteError? error)
    {
        var state = result.State;

        if (state.Session.Operations.Any(operation => operation.Id == operationId))
        {
            return result;
        }

        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Form?.OperationId == operationId);

        if (tab is null)
        {
            return result;
        }

        var settled = applied ? Closed(state, tab) : Unlocked(state, tab, error);

        return result with { State = settled };
    }

    internal static ReduceResult Open(AppState state, TabId tabId, FormTarget target)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab is null)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var form = FormState.Opened(target, requestId, BrowserUrl(state, target));
        var opened = ViewReducer.Replace(state, tab with { Form = form });
        var next = opened with
        {
            Mode = InputMode.Insert,
            Sequence = requestId,
            Message = null,
            PendingKeys = string.Empty,
        };

        return new ReduceResult(next, [new LoadFormMetadataEffect(tabId, requestId, target)]);
    }

    /// <summary>
    /// Opens the people picker at once: there is no metadata to fetch, the people come from a search of the typed
    /// text. It starts with the account of the profile and the current assignee.
    /// </summary>
    internal static ReduceResult OpenAssign(AppState state, TabId tabId, AssignFormTarget target)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab is null)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var field = AssigneeField(state, target);
        var opened = FormState.Opened(target, requestId, BrowserUrl(state, target));
        var form = opened with { Fields = [field], IsLoading = false };
        var next = ViewReducer.Replace(state, tab with { Form = form }) with
        {
            Mode = InputMode.Insert,
            Sequence = requestId,
            Message = null,
            PendingKeys = string.Empty,
        };

        return Result(next);
    }

    private static FormField AssigneeField(AppState state, AssignFormTarget target)
    {
        var suggested = new List<FieldOption>();

        if (state.Session.Connection is Connected { AccountId: { } accountId } connected)
        {
            suggested.Add(new FieldOption(accountId, connected.DisplayName));
        }

        if (target.Current is { } current && suggested.All(option => option.Id != current.AccountId))
        {
            suggested.Add(new FieldOption(current.AccountId, current.DisplayName));
        }

        var field = FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.Select, false, suggested) with
        {
            NoneLabel = "Unassigned",
            Searchable = true,
            Suggested = suggested,
        };

        return target.Current is { } assignee ? field.WithInitial(FormValue.OfChosen([assignee.AccountId])) : field;
    }

    private static FieldChange ChangeOf(FormField field)
    {
        var display = FormFieldKinds.IsChoice(field.Kind)
            ? field.Value.Chosen.Select(id => LabelOf(field, id)).ToArray()
            : null;

        return new FieldChange(field.Id, field.Kind, field.Value, field.Initial, display);
    }

    private static string LabelOf(FormField field, string id)
    {
        return field.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id;
    }

    /// <summary>
    /// After an edit of the focused field: a searchable field whose typed text changed asks for new options (an
    /// empty text asks for none and drops any search still running). The request id makes older answers stale.
    /// </summary>
    private static ReduceResult Searched(AppState before, AppState after)
    {
        var tab = ActiveTab(after);
        var old = ActiveTab(before)?.Form;

        if (tab?.Form is not { } form || old is null || form.Focus >= form.Fields.Count)
        {
            return Result(after);
        }

        if (form.Target.IssueKey is not { } key)
        {
            return Result(after);
        }

        var field = form.Fields[form.Focus];

        if (!field.Searchable || field.Entry == old.Fields[old.Focus].Entry)
        {
            return Result(after);
        }

        var requestId = after.Sequence + 1;
        var next = Replace(after with { Sequence = requestId }, tab, form with { SearchRequestId = requestId });
        var query = field.Entry.Trim();

        if (query.Length == 0)
        {
            return Result(next);
        }

        return new ReduceResult(next, [new SearchFieldOptionsEffect(tab.Id, requestId, key, query)]);
    }

    private static ReduceResult Found(AppState state, FieldOptionsLoaded found)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, found.Tab);

        if (tab?.Form is not { } form || form.SearchRequestId != found.RequestId || form.Focus >= form.Fields.Count)
        {
            return Result(state);
        }

        var field = form.Fields[form.Focus];

        if (!field.Searchable)
        {
            return Result(state);
        }

        var fields = WithField(form.Fields, form.Focus, FormEditor.WithOptions(field, found.Options));

        return Result(Replace(state, tab, form with { Fields = fields, TopError = null }));
    }

    private static ReduceResult SearchFailed(AppState state, FieldOptionsFailed failed)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, failed.Tab);

        if (tab?.Form is not { } form || form.SearchRequestId != failed.RequestId)
        {
            return Result(state);
        }

        return Result(Replace(state, tab, form with { TopError = failed.Message }));
    }

    private static ReduceResult OpenLabels(AppState state)
    {
        var tab = ActiveTab(state);
        var key = tab is null ? null : TargetKey(state, tab);

        if (tab is null || key is null)
        {
            return Say(state, NoIssueText);
        }

        var labels = LabelsOf(tab, key);

        if (labels is null)
        {
            return Say(state, $"The labels of {key} are not loaded yet");
        }

        var known = new Dictionary<string, FormValue> { [FormFieldIds.Labels] = FormValue.OfChosen(labels) };
        var target = new EditFormTarget(key, known, true, FormFieldKind.Labels);

        return Open(state, tab.Id, target);
    }

    private static string? TargetKey(AppState state, TabState tab)
    {
        return tab.Detail.Count > 0 ? tab.Detail[^1].Key : BrowserReducer.SelectedKey(state);
    }

    private static IReadOnlyList<string>? LabelsOf(TabState tab, string key)
    {
        if (tab.Detail.Count > 0)
        {
            var detail = tab.Detail[^1];

            return detail.Issue?.Labels ?? detail.Row?.Labels;
        }

        return tab.Rows.FirstOrDefault(row => string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase))?.Labels;
    }

    private static string? BrowserUrl(AppState state, FormTarget target)
    {
        var profile = state.Profiles.Active;

        if (target.IssueKey is not { } key || profile is null)
        {
            return null;
        }

        if (!Uri.TryCreate(profile.SiteUrl, UriKind.Absolute, out var site))
        {
            return null;
        }

        return IssueUrls.Browse(new JiraConnection(site, profile.Email), key)?.AbsoluteUri;
    }

    private static ReduceResult Loaded(AppState state, FormMetadataLoaded loaded)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);

        if (tab?.Form is not { IsLoading: true } form || form.RequestId != loaded.RequestId)
        {
            return Result(state);
        }

        var top = loaded.Fields.Any(field => field.IsEditable) ? null : NoFieldsText;
        var filled = form with { Fields = loaded.Fields, IsLoading = false, TopError = top };

        return Result(Replace(state, tab, filled with { Focus = Step(filled, 0) }));
    }

    private static ReduceResult Failed(AppState state, FormMetadataFailed failed)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, failed.Tab);

        if (tab?.Form is not { IsLoading: true } form || form.RequestId != failed.RequestId)
        {
            return Result(state);
        }

        return Result(Replace(state, tab, form with { IsLoading = false, TopError = failed.Message }));
    }

    /// <summary>While the save runs the form ignores edits; Esc closes it, the result goes to the status line.</summary>
    private static ReduceResult Saving(AppState state, TabState tab, string? action)
    {
        if (action != ActionIds.Cancel)
        {
            return Result(state);
        }

        return Result(Closed(state, tab) with { Message = DetachedText });
    }

    private static ReduceResult Discarding(AppState state, TabState tab, string? answer, string? action)
    {
        if (answer == ActionIds.ConfirmYes)
        {
            return Result(Closed(state, tab) with { Message = "Discarded the changes" });
        }

        if (answer == ActionIds.ConfirmNo || action == ActionIds.Cancel)
        {
            return Result(Replace(state, tab, tab.Form! with { DiscardPending = false }));
        }

        return Result(state);
    }

    private static ReduceResult Cancel(AppState state, TabState tab, FormState form)
    {
        if (form.IsDirty)
        {
            return Result(Replace(state, tab, form with { DiscardPending = true }));
        }

        return Result(Closed(state, tab));
    }

    private static ReduceResult Enter(AppState state, TabState tab, FormState form, DateTimeOffset at)
    {
        if (form.Fields.Count == 0 || form.Focus >= form.Fields.Count)
        {
            return Result(state);
        }

        var edit = FormEditor.Accept(form.Fields[form.Focus]);
        var fields = WithField(form.Fields, form.Focus, edit.Field);
        var edited = form with { Fields = fields };

        if (!edit.Advance)
        {
            return Result(Replace(state, tab, edited));
        }

        var next = NextEditable(edited, form.Focus);

        if (next is null)
        {
            return Submit(Replace(state, tab, edited), tab, edited, at);
        }

        return Result(Replace(state, tab, edited with { Focus = next.Value }));
    }

    private static ReduceResult Submit(AppState state, TabState tab, FormState form, DateTimeOffset at)
    {
        if (form.IsLoading || !form.Fields.Any(field => field.IsEditable))
        {
            return Result(state);
        }

        var committed = form.Fields.Select(FormEditor.Committed).ToArray();
        var checkedFields = FormValidation.Check(committed);
        var firstError = IndexOfError(checkedFields);

        if (firstError >= 0)
        {
            var invalid = form with { Fields = checkedFields, Focus = firstError, TopError = null };

            return Result(Replace(state, tab, invalid));
        }

        var changes = checkedFields
            .Where(field => field.IsChanged)
            .Select(ChangeOf)
            .ToArray();

        if (changes.Length == 0)
        {
            return Result(Replace(state, tab, form with { Fields = checkedFields, TopError = NothingChangedText }));
        }

        return Send(state, tab, form with { Fields = checkedFields, TopError = null }, changes, at);
    }

    private static ReduceResult Send(
        AppState state,
        TabState tab,
        FormState form,
        IReadOnlyList<FieldChange> changes,
        DateTimeOffset at)
    {
        var request = form.Target.Request(changes);
        var started = WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));

        if (started.State.Session.Operations.Count <= state.Session.Operations.Count)
        {
            // Refused (busy, no profile): the form stays as it is, with the reason on its top line.
            var refused = form with { TopError = started.State.Message };

            return Result(Replace(state, tab, refused));
        }

        var locked = form with { OperationId = started.State.Sequence };

        return started with { State = Replace(started.State, tab, locked) };
    }

    private static AppState Closed(AppState state, TabState tab)
    {
        var current = ViewReducer.Find(state.Session.Tabs, tab.Id) ?? tab;
        var closed = ViewReducer.Replace(state, current with { Form = null });
        var active = closed.Session.Active == tab.Id && closed.Mode == InputMode.Insert;

        return active ? closed with { Mode = InputMode.Normal, PendingKeys = string.Empty } : closed;
    }

    private static AppState Unlocked(AppState state, TabState tab, WriteError? error)
    {
        var form = tab.Form!;
        var fields = form.Fields.Select(field => field with { Error = FieldErrorOf(error, field) }).ToArray();
        var focus = IndexOfError(fields);
        var mapped = fields.Count(field => field.Error is not null);
        var allMapped = error is not null && mapped == error.FieldErrors.Count && error.Messages.Count == 0;
        var top = allMapped && mapped > 0 ? null : state.Message;

        var unlocked = form with
        {
            OperationId = null,
            Fields = fields,
            Focus = focus >= 0 ? focus : form.Focus,
            TopError = top,
        };

        return ViewReducer.Replace(state, tab with { Form = unlocked });
    }

    private static string? FieldErrorOf(WriteError? error, FormField field)
    {
        return error?.FieldErrors.FirstOrDefault(candidate => candidate.FieldId == field.Id)?.Message;
    }

    private static int IndexOfError(IReadOnlyList<FormField> fields)
    {
        for (var index = 0; index < fields.Count; index++)
        {
            if (fields[index].Error is not null)
            {
                return index;
            }
        }

        return -1;
    }

    private static AppState Edit(AppState state, TabState tab, FormState form, Func<FormField, FormField> edit)
    {
        if (form.Fields.Count == 0 || form.Focus >= form.Fields.Count)
        {
            return state;
        }

        var field = edit(form.Fields[form.Focus]);

        return Replace(state, tab, form with { Fields = WithField(form.Fields, form.Focus, field), TopError = null });
    }

    private static AppState Typed(AppState state, TabState tab, FormState form, string? text)
    {
        return text is null ? state : Edit(state, tab, form, field => FormEditor.Type(field, text));
    }

    private static FormField[] WithField(IReadOnlyList<FormField> fields, int index, FormField field)
    {
        var copy = fields.ToArray();

        copy[index] = field;

        return copy;
    }

    /// <summary>The focus after moving <paramref name="delta"/> editable fields (0: the first editable one).</summary>
    private static int Step(FormState form, int delta)
    {
        var editable = Enumerable.Range(0, form.Fields.Count).Where(index => form.Fields[index].IsEditable).ToArray();

        if (editable.Length == 0)
        {
            return 0;
        }

        var position = Array.IndexOf(editable, form.Focus);

        if (delta == 0 || position < 0)
        {
            return editable[0];
        }

        return editable[(position + delta + editable.Length) % editable.Length];
    }

    private static int? NextEditable(FormState form, int from)
    {
        for (var index = from + 1; index < form.Fields.Count; index++)
        {
            if (form.Fields[index].IsEditable)
            {
                return index;
            }
        }

        return null;
    }

    // The tab is looked up again: the state may have changed since the caller read it (a write patched its rows).
    private static AppState Replace(AppState state, TabState tab, FormState form)
    {
        var current = ViewReducer.Find(state.Session.Tabs, tab.Id) ?? tab;

        return ViewReducer.Replace(state, current with { Form = form });
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    private static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
