using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
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
public static partial class FormReducer
{
    private const string NoIssueText = "No issue selected: select a row or open an issue first";
    private const string NoPageText = "No issue page to open";
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
            OpenCreateFormRequested create => CreateFormReducer.Open(state, create.Subtask),
            FormTypesLoaded types => CreateFormReducer.TypesLoaded(state, types),
            OpenLinkFormRequested => OpenLink(state),
            FormMetadataLoaded loaded => Loaded(state, loaded),
            FormMetadataFailed failed => Failed(state, failed),
            SuggestionsLoaded loaded => Suggested(state, loaded.Tab, SuggestionLoaded(loaded)),
            SuggestionsSlow slow => Suggested(state, slow.Tab, SuggestionSlow(slow)),
            SuggestionsFailed failed => Suggested(state, failed.Tab, SuggestionFailed(failed)),
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

        if (form.IsChoosingType)
        {
            return CreateFormReducer.ChoosingType(state, tab, form, action);
        }

        if (form.Suggest is { } popup && Suggesting(state, tab, form, popup, action, at) is { } handled)
        {
            return Choosing(handled, tab.Id);
        }

        var result = action switch
        {
            ActionIds.Cancel => Cancel(state, tab, form),
            ActionIds.FormOpenAfter => Result(CreateFormReducer.ToggleOpenAfter(state, tab, form)),
            ActionIds.FormSubmit => Submit(state, tab, form, at),
            ActionIds.FormOpenBrowser => OpenBrowser(state, form),
            ActionIds.FormFieldNext => Result(Replace(state, tab, form with { Focus = Step(form, 1) })),
            ActionIds.FormFieldPrevious => Result(Replace(state, tab, form with { Focus = Step(form, -1) })),
            ActionIds.FormNext => Enter(state, tab, form, at),
            ActionIds.FormBackspace => Edited(Edit(state, tab, form, FormEditor.Backspace), tab.Id),
            null => Edited(Typed(state, tab, form, KeyNotation.ToText(key)), tab.Id),
            _ => Result(state),
        };

        return Choosing(LeavesField(action) ? Unsuggested(result, tab.Id) : result, tab.Id);
    }

    /// <summary>Pasted text goes to the focused field (first line only).</summary>
    public static ReduceResult Paste(AppState state, string text)
    {
        var tab = ActiveTab(state)!;
        var form = tab.Form!;

        return form.IsSaving || form.DiscardPending || text.Length == 0
            ? Result(state)
            : Choosing(Edited(Typed(state, tab, form, text), tab.Id), tab.Id);
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

        if (applied)
        {
            return result with { State = Closed(state, tab) };
        }

        // Unlocked: the focused choice field shows its list again.
        return Choosing(result with { State = Unlocked(state, tab, error) }, tab.Id);
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
    /// text (the suggestion popup). It starts on the current assignee.
    /// </summary>
    internal static ReduceResult OpenAssign(AppState state, TabId tabId, AssignFormTarget target)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab is null)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var field = AssigneeField(target);
        var opened = FormState.Opened(target, requestId, BrowserUrl(state, target));
        var form = WithQuickRows(state, opened with { Fields = [field], IsLoading = false });
        var next = ViewReducer.Replace(state, tab with { Form = form }) with
        {
            Mode = InputMode.Insert,
            Sequence = requestId,
            Message = null,
            PendingKeys = string.Empty,
        };

        return Result(next);
    }

    // The assignee is a person field like any other: typing opens the suggestion popup, which searches the people who
    // can be assigned. Backspace on an empty buffer clears it (unassigned).
    private static FormField AssigneeField(AssignFormTarget target)
    {
        List<FieldOption> known = [];

        if (target.Current is { } assignee)
        {
            known.Add(new FieldOption(assignee.AccountId, assignee.DisplayName));
        }

        var field = FormField.Create(FormFieldIds.Assignee, "Assignee", FormFieldKind.User, false, known);

        return target.Current is { } current ? field.WithInitial(FormValue.OfChosen([current.AccountId])) : field;
    }

    private static FieldChange ChangeOf(FormField field)
    {
        var display = FormFieldKinds.IsChoice(field.Kind) || FormFieldKinds.IsPicked(field.Kind)
            ? field.Value.Chosen.Select(id => LabelOf(field, id)).ToArray()
            : null;

        return new FieldChange(field.Id, field.Kind, field.Value, field.Initial, display);
    }

    private static string LabelOf(FormField field, string id)
    {
        return field.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id;
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

    private static ReduceResult OpenLink(AppState state)
    {
        var tab = ActiveTab(state);
        var key = tab is null ? null : TargetKey(state, tab);

        return tab is null || key is null ? Say(state, NoIssueText) : Open(state, tab.Id, new LinkFormTarget(key));
    }

    internal static string? TargetKey(AppState state, TabState tab)
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

    private static ReduceResult OpenBrowser(AppState state, FormState form)
    {
        if (form.BrowserUrl is not { } url)
        {
            return Result(state with { Message = NoPageText });
        }

        return BrowserReducer.Request(state, new OpenUrlRequested(url, false, form.Target.IssueKey));
    }

    private static string? BrowserUrl(AppState state, FormTarget target)
    {
        var profile = state.Profiles.Active;

        if (profile is null || !Uri.TryCreate(profile.SiteUrl, UriKind.Absolute, out var site))
        {
            return null;
        }

        var connection = new JiraConnection(site, profile.Email);

        // A create has no issue yet: the link goes to the project, where the browser form has every field.
        if (target is CreateFormTarget create)
        {
            return IssueUrls.BrowseProject(connection, create.ProjectKey)?.AbsoluteUri;
        }

        return target.IssueKey is { } key ? IssueUrls.Browse(connection, key)?.AbsoluteUri : null;
    }

    private static ReduceResult Loaded(AppState state, FormMetadataLoaded loaded)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, loaded.Tab);

        if (tab?.Form is not { IsLoading: true } form || form.RequestId != loaded.RequestId)
        {
            return Result(state);
        }

        var target = (loaded.Target ?? form.Target).Loaded(loaded.Fields);
        var top = loaded.Fields.Any(field => field.IsEditable) ? null : target.EmptyText;
        var filled = form with
        {
            Target = target,
            Fields = loaded.Fields,
            IsLoading = false,
            TopError = top,
        };
        var shown = filled with { Focus = Step(filled, 0) };

        if (target.Automatic is { } changes)
        {
            return Send(Replace(state, tab, shown), tab, shown, changes, loaded.At);
        }

        return Choosing(Result(Replace(state, tab, shown)), tab.Id);
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

        if (changes.Length == 0 && !form.Target.AllowsNoChanges)
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
        if (form.Target.Following(changes) is { } following)
        {
            return Open(state, tab.Id, following);
        }

        var request = form.Target.Request(changes);
        var started = WriteReducer.Reduce(state, new WriteRequested(tab.Id, request, at));

        if (state.Confirm is null && started.State.Confirm is not null)
        {
            // The write asks first: the form is done, the answer decides.
            return started with { State = Closed(started.State, tab) };
        }

        if (started.State.Session.Operations.Count <= state.Session.Operations.Count)
        {
            // Refused (busy, no profile): the form stays as it is, with the reason on its top line.
            var refused = form with { TopError = started.State.Message };

            return Result(Replace(state, tab, refused));
        }

        if (form.Target.ClosesOnSend)
        {
            return started with { State = Closed(started.State, tab) };
        }

        var locked = form with { OperationId = started.State.Sequence };

        return started with { State = Replace(started.State, tab, locked) };
    }

    internal static AppState Closed(AppState state, TabState tab)
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
    internal static AppState Replace(AppState state, TabState tab, FormState form)
    {
        var current = ViewReducer.Find(state.Session.Tabs, tab.Id) ?? tab;

        return ViewReducer.Replace(state, current with { Form = form });
    }

    internal static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }

    internal static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }

    internal static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }

    // A result that also stops the lookup of the popup that was just closed.
    private static ReduceResult Result(AppState state, TabId cancelled)
    {
        return new ReduceResult(state, [new CancelSuggestionsEffect(cancelled)]);
    }
}
