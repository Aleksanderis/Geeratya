using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The suggestion popup of the form (<c>docs/internal/ux-workflows.md</c>, "Suggestions"): it opens by itself when text is
/// typed into a labels, user or parent field, follows the typing buffer, and closes on Esc, on accepting a row, or
/// when the focus leaves the field.
/// </summary>
public static partial class FormReducer
{
    private const int KnownLabelLimit = 500;

    // The keys of an open popup: Esc closes it alone (the quick rows of an empty field leave Esc to the form), Up and
    // Down move, Tab and Enter take the highlighted row (Enter on a full key typed into the link form's issue field takes that key, Tab the row). Any other key is not the popup's (null) and goes on to the field.
    private static ReduceResult? Suggesting(
        AppState state,
        TabState tab,
        FormState form,
        SuggestionState popup,
        string? action,
        DateTimeOffset at)
    {
        return action switch
        {
            ActionIds.Cancel when popup.Query.Length == 0 => null,
            ActionIds.Cancel when FormFieldKinds.IsChoice(form.Fields[form.Focus].Kind)
                => Result(ClearedFilter(state, tab, form)),
            ActionIds.Cancel => Result(Replace(state, tab, form with { Suggest = null }), tab.Id),
            ActionIds.PickerNext => Moved(state, tab, form, popup, 1),
            ActionIds.PickerPrevious => Moved(state, tab, form, popup, -1),
            ActionIds.FormFieldNext when popup.Source.Kind == SuggestionKind.Options && popup.Query.Length == 0 => null,
            ActionIds.FormNext when FormEditor.TypedKey(form.Fields[form.Focus]) is not null => null,
            ActionIds.FormNext or ActionIds.FormFieldNext when popup.Selected is { } picked
                => Accepting(state, tab, form, picked, action == ActionIds.FormNext, at),
            _ => null,
        };
    }

    // A person, issue or label stays in its field after it is taken (Enter on the empty buffer then moves on); a
    // single choice is done once taken, so Enter or Tab go on to the next field (Enter saves from the last one).
    private static ReduceResult Accepting(
        AppState state,
        TabState tab,
        FormState form,
        Suggestion picked,
        bool enter,
        DateTimeOffset at)
    {
        var taken = Accepted(form, picked);

        // A dropdown under a detail field writes as soon as a row is taken (the popup is the whole form).
        if (form.Fields[form.Focus].Kind != FormFieldKind.Select && !(enter && form.Dropdown is not null))
        {
            return Result(Replace(state, tab, taken), tab.Id);
        }

        if (enter)
        {
            return Enter(Replace(state, tab, taken), tab, taken, at);
        }

        return Result(Replace(state, tab, taken with { Focus = Step(taken, 1) }));
    }

    // Esc in a choice field drops the typed filter; the list of its values stays.
    private static AppState ClearedFilter(AppState state, TabState tab, FormState form)
    {
        var field = form.Fields[form.Focus] with { Entry = string.Empty, Back = 0 };

        return Replace(state, tab, form with { Fields = WithField(form.Fields, form.Focus, field) });
    }

    // A choice field always shows its popup while it has the focus; any other field leaves a stale one to Edited.
    private static ReduceResult Choosing(ReduceResult result, TabId tabId)
    {
        var tab = ViewReducer.Find(result.State.Session.Tabs, tabId);

        if (tab?.Form is not { } form || form.Fields.Count == 0 || form.Focus >= form.Fields.Count)
        {
            return result;
        }

        var field = form.Fields[form.Focus];

        if (!FormFieldKinds.IsChoice(field.Kind) || !field.IsEditable || form.IsSaving || form.Navigating)
        {
            return form.Suggest is { Source.Kind: SuggestionKind.Options }
                ? new ReduceResult(Replace(result.State, tab, form with { Suggest = null }), result.Effects)
                : result;
        }

        // A typed filter of a field with a project list is answered by that search, not by the local values.
        if (field.Lookup is not null && field.Entry.Trim().Length > 0
            && form.Suggest is { Source.Kind: not SuggestionKind.Options })
        {
            return result;
        }

        var popup = ChoicePopup.For(field, form.Focus, form.Suggest);

        return ReferenceEquals(popup, form.Suggest)
            ? result
            : new ReduceResult(Replace(result.State, tab, form with { Suggest = popup }), result.Effects);
    }

    private static ReduceResult Moved(AppState state, TabState tab, FormState form, SuggestionState popup, int delta)
    {
        return Result(Replace(state, tab, form with { Suggest = SuggestionPopup.Moved(popup, delta) }));
    }

    private static FormState Accepted(FormState form, Suggestion picked)
    {
        var field = FormEditor.Choose(form.Fields[form.Focus], picked);

        return form with { Fields = WithField(form.Fields, form.Focus, field), TopError = null, Suggest = null };
    }

    private static bool LeavesField(string? action)
    {
        return action is ActionIds.FormSubmit or ActionIds.FormFieldNext or ActionIds.FormFieldPrevious
            or ActionIds.FormNext;
    }

    private static ReduceResult Unsuggested(ReduceResult result, TabId tabId)
    {
        var tab = ViewReducer.Find(result.State.Session.Tabs, tabId);

        if (tab?.Form?.Suggest is null)
        {
            return result;
        }

        var closed = Replace(result.State, tab, tab.Form with { Suggest = null });

        // The quick rows (request 0) have no lookup to cancel.
        return tab.Form.Suggest.RequestId == 0
            ? new ReduceResult(closed, result.Effects)
            : new ReduceResult(closed, [.. result.Effects, new CancelSuggestionsEffect(tabId)]);
    }

    // After an edit the popup follows the focused field's typing buffer: a new query matches at once against what is
    // loaded and asks for a lookup (debounced by the host); an empty buffer closes the popup.
    private static ReduceResult Edited(AppState state, TabId tabId)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab?.Form is not { } form || form.Fields.Count == 0 || form.Focus >= form.Fields.Count)
        {
            return Result(state);
        }

        var field = form.Fields[form.Focus];
        var query = field.Entry.Trim();

        if (query.Length == 0 && QuickPopup(state, form) is { } quick)
        {
            return QuickShown(state, tab, form, quick);
        }

        var source = query.Length == 0 ? null : SourceOf(tab, form, field);

        if (source is null)
        {
            return Unsuggested(Result(state), tabId);
        }

        if (form.Suggest is { } open && open.FieldIndex == form.Focus && open.Query == query)
        {
            return Result(state);
        }

        var requestId = state.Sequence + 1;
        var popup = SuggestionPopup.Queried(form.Suggest, form.Focus, source, query, field.Value.Chosen, requestId);
        var next = Replace(state with { Sequence = requestId }, tab, form with { Suggest = popup });
        var effect = new SearchSuggestionsEffect(tabId, requestId, source, query, SuggestionPopup.Debounce);

        return new ReduceResult(next, [effect]);
    }

    // The assign picker offers "Me" and "Unassigned" while its buffer is empty: no lookup, nothing highlighted, so
    // Enter still saves the form as it stands. Typing replaces them with the single search path. Returns null for any
    // other field, or when neither row applies (already assigned to me and no one to clear).
    private static SuggestionState? QuickPopup(AppState state, FormState form)
    {
        if (LabelRows(form) is { } labels)
        {
            return labels;
        }

        if (form.Target is not AssignFormTarget || form.Focus >= form.Fields.Count
            || form.Fields[form.Focus].Id != FormFieldIds.Assignee)
        {
            return null;
        }

        var chosen = form.Fields[form.Focus].Value.Chosen;
        var rows = new List<Suggestion>();

        if (state.Session.Connection is Connected { AccountId: { } accountId } connected && !chosen.Contains(accountId))
        {
            rows.Add(AssigneeQuickRows.Me(accountId, connected.DisplayName));
        }

        if (chosen.Count > 0)
        {
            rows.Add(AssigneeQuickRows.Unassigned);
        }

        if (rows.Count == 0)
        {
            return null;
        }

        var source = new SuggestionSource(SuggestionKind.AssignableUsers, form.Target.IssueKey, []);

        return new SuggestionState(form.Focus, source, string.Empty, [], rows, -1, SuggestionStatus.Loaded, 0, null);
    }

    // The labels dropdown of the Details pane lists the labels the issue has while the buffer is empty, so one can be
    // taken off; null for any other form or when there is none.
    private static SuggestionState? LabelRows(FormState form)
    {
        if (form.Dropdown is null || form.Focus >= form.Fields.Count)
        {
            return null;
        }

        var field = form.Fields[form.Focus];

        if (field.Kind != FormFieldKind.Labels || field.Value.Chosen.Count == 0)
        {
            return null;
        }

        var rows = field.Value.Chosen.Select(LabelQuickRows.Applied).ToArray();
        var source = new SuggestionSource(SuggestionKind.Labels, null, []);

        return new SuggestionState(form.Focus, source, string.Empty, [], rows, -1, SuggestionStatus.Loaded, 0, null);
    }

    internal static FormState WithQuickRows(AppState state, FormState form)
    {
        return QuickPopup(state, form) is { } quick ? form with { Suggest = quick } : form;
    }

    private static ReduceResult QuickShown(AppState state, TabState tab, FormState form, SuggestionState quick)
    {
        if (form.Suggest is { Query.Length: 0 } open && open.FieldIndex == form.Focus
            && open.Items.Select(item => item.Id).SequenceEqual(quick.Items.Select(item => item.Id)))
        {
            return Result(state);
        }

        var next = Replace(state, tab, form with { Suggest = quick });
        var effects = form.Suggest is null or { RequestId: 0 } ? _noEffects : new Effect[] { new CancelSuggestionsEffect(tab.Id) };

        return new ReduceResult(next, effects);
    }

    private static SuggestionSource? SourceOf(TabState tab, FormState form, FormField field)
    {
        if (field.Kind == FormFieldKind.Labels)
        {
            var known = tab.Rows.SelectMany(row => row.Labels).Distinct(StringComparer.Ordinal).Take(KnownLabelLimit);

            return new SuggestionSource(SuggestionKind.Labels, null, [.. known]);
        }

        if (form.Target is CreateFormTarget create)
        {
            return CreateSourceOf(create, field);
        }

        if (form.Target.IssueKey is not { } key)
        {
            return null;
        }

        return field.Kind switch
        {
            FormFieldKind.User => new SuggestionSource(UserKind(field), key, []),
            // The parent of an edited issue is searched in its project; a link target may be in any project.
            FormFieldKind.Parent => new SuggestionSource(
                SuggestionKind.Issues,
                key,
                [],
                form.Target is EditFormTarget ? JumpKey.ProjectOf(key) : null,
                (form.Target as EditFormTarget)?.ParentLevel),
            _ => null,
        };
    }

    // A create form has no issue yet: people who can be assigned in the project (the assignee), people who can see its
    // issues (other person fields), the components and versions of the project (the typed filter of those fields) and
    // issues of the project as parent.
    private static SuggestionSource? CreateSourceOf(CreateFormTarget create, FormField field)
    {
        if (field.Lookup is { } lookup && FormFieldKinds.IsChoice(field.Kind))
        {
            return new SuggestionSource(lookup, null, [], create.ProjectKey);
        }

        return field.Kind switch
        {
            FormFieldKind.User when field.Id == FormFieldIds.Assignee
                => new SuggestionSource(SuggestionKind.AssignableUsers, null, [], create.ProjectKey),
            FormFieldKind.User => new SuggestionSource(SuggestionKind.ProjectUsers, null, [], create.ProjectKey),
            FormFieldKind.Parent => new SuggestionSource(
                SuggestionKind.Issues,
                null,
                [],
                create.ProjectKey,
                create.IssueType is { } type ? ParentLevels.Accepted(type.HierarchyLevel, type.IsSubtask) : null),
            _ => null,
        };
    }

    private static SuggestionKind UserKind(FormField field)
    {
        return field.Id == FormFieldIds.Assignee ? SuggestionKind.AssignableUsers : SuggestionKind.IssueUsers;
    }

    private static Func<SuggestionState, FormField, SuggestionState?> SuggestionLoaded(SuggestionsLoaded loaded)
    {
        return (popup, field) => SuggestionPopup.Loaded(popup, loaded.RequestId, loaded.Items, field.Value.Chosen);
    }

    private static Func<SuggestionState, FormField, SuggestionState?> SuggestionSlow(SuggestionsSlow slow)
    {
        return (popup, _) => SuggestionPopup.Slow(popup, slow.RequestId);
    }

    private static Func<SuggestionState, FormField, SuggestionState?> SuggestionFailed(SuggestionsFailed failed)
    {
        return (popup, _) => SuggestionPopup.Failed(popup, failed.RequestId, failed.Message);
    }

    // Applies a lookup answer to the popup that still waits for it: same request, same focused field.
    private static ReduceResult Suggested(
        AppState state,
        TabId tabId,
        Func<SuggestionState, FormField, SuggestionState?> apply)
    {
        var tab = ViewReducer.Find(state.Session.Tabs, tabId);

        if (tab?.Form is not { Suggest: { } popup } form || form.Focus != popup.FieldIndex
            || popup.FieldIndex >= form.Fields.Count)
        {
            return Result(state);
        }

        var next = apply(popup, form.Fields[popup.FieldIndex]);

        return next is null ? Result(state) : Result(Replace(state, tab, form with { Suggest = next }));
    }
}
