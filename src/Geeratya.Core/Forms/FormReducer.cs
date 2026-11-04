using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Panes;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
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

        if (form.Navigating)
        {
            return Navigating(state, tab, form, key, action, at, keymap);
        }

        var result = action switch
        {
            ActionIds.Cancel when form.Navigable
                => Result(Replace(state, tab, form with { Navigating = true, Suggest = null }), tab.Id),
            ActionIds.Cancel => Cancel(state, tab, form),
            ActionIds.FormOpenAfter => Result(CreateFormReducer.ToggleOpenAfter(state, tab, form)),
            ActionIds.FormMoreFields => Result(Replace(state, tab, CreatePanes.ToggleMore(form))),
            ActionIds.FormSubmit => Submit(state, tab, form, at),
            ActionIds.FormOpenBrowser => OpenBrowser(state, form),
            ActionIds.FormFieldNext => Result(Replace(state, tab, form with { Focus = Step(form, 1) })),
            ActionIds.FormFieldPrevious => Result(Replace(state, tab, form with { Focus = Step(form, -1) })),
            ActionIds.FormNext => Enter(state, tab, form, at),
            ActionIds.FormBackspace => Edited(Edit(state, tab, form, FormEditor.Backspace), tab.Id),
            null => Edited(Typed(state, tab, form, KeyNotation.ToText(key)), tab.Id),
            _ when PromptEditing.OperationOf(action) is { } operation
                => Edited(Edit(state, tab, form, field => FormEditor.Apply(field, operation)), tab.Id),
            _ => Result(state),
        };

        return Choosing(LeavesField(action) ? Unsuggested(result, tab.Id) : result, tab.Id);
    }

    // The navigate state of the edit form (#650): fields are stops, nothing is typed. Enter or i starts editing.
    private static ReduceResult Navigating(
        AppState state,
        TabState tab,
        FormState form,
        string key,
        string? action,
        DateTimeOffset at,
        KeymapTable keymap)
    {
        var motion = keymap.Resolve(InputMode.Normal, string.Empty, key).Action;

        if (form.Target is CreateFormTarget && CreateNavigating(state, tab, form, motion, action) is { } created)
        {
            return created;
        }

        // Pane focus first (decision 2026-11-04, "pane keys in forms"): with panes on the tab, Ctrl-h/j/k/l move
        // between panes; without them the keys keep their old meaning below (Ctrl-j/k step the field).
        if (PaneActions.Apply(state, motion) is { } focused)
        {
            return Result(focused);
        }

        if (action == ActionIds.FormFieldNext || motion == ActionIds.NavigateDown || action == ActionIds.PickerNext)
        {
            return Result(Replace(state, tab, form with { Focus = Step(form, 1) }));
        }

        if (action == ActionIds.FormFieldPrevious || motion == ActionIds.NavigateUp || action == ActionIds.PickerPrevious)
        {
            return Result(Replace(state, tab, form with { Focus = Step(form, -1) }));
        }

        if (action == ActionIds.FormNext || key == "i")
        {
            return Choosing(Result(Replace(state, tab, form with { Navigating = false })), tab.Id);
        }

        return action switch
        {
            ActionIds.Cancel => Cancel(state, tab, form),
            ActionIds.FormSubmit => Submit(state, tab, form, at),
            ActionIds.FormOpenBrowser => OpenBrowser(state, form),
            _ => Result(state),
        };
    }

    // The navigate state of the create form: the three panes of CreatePanes, Ctrl-f and Ctrl-g work here too.
    private static ReduceResult? CreateNavigating(
        AppState state,
        TabState tab,
        FormState form,
        string? motion,
        string? action)
    {
        if (action == ActionIds.FormMoreFields)
        {
            return Result(Replace(state, tab, CreatePanes.ToggleMore(form)));
        }

        if (action == ActionIds.FormOpenAfter)
        {
            return Result(CreateFormReducer.ToggleOpenAfter(state, tab, form));
        }

        if (CreatePanes.Move(form, DetailSize.Columns(state), motion) is not { } focus)
        {
            return null;
        }

        return focus == form.Focus ? Result(state) : Result(Replace(state, tab, form with { Focus = focus }));
    }

    /// <summary>Pasted text goes to the focused field (first line only).</summary>
    public static ReduceResult Paste(AppState state, string text)
    {
        var tab = ActiveTab(state)!;
        var form = tab.Form!;

        return form.IsSaving || form.DiscardPending || form.Navigating || text.Length == 0
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
        var editable = Enumerable.Range(0, form.Fields.Count)
            .Where(index => form.Fields[index].IsEditable && !CreatePanes.IsFolded(form, index))
            .ToArray();

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
