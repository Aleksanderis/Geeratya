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
    // Down move, Tab and Enter take the highlighted row (Enter on a full issue key typed into any parent field takes that key, Tab the row). Any other key is not the popup's (null) and goes on to the field.
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

        if (form.Suggest is null && field.Entry.Length == 0 && QuickPopup(result.State, form) is { } quick)
        {
            return new ReduceResult(Replace(result.State, tab, form with { Suggest = quick }), result.Effects);
        }

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
        // Quick rows (request 0) hold no pool of their own: the search starts from the source's known values.
        var previous = form.Suggest is { RequestId: 0 } ? null : form.Suggest;
        var popup = SuggestionPopup.Queried(previous, form.Focus, source, query, field.Value.Chosen, requestId);
        var next = Replace(state with { Sequence = requestId }, tab, form with { Suggest = popup });
        var effect = new SearchSuggestionsEffect(tabId, requestId, source, query, SuggestionPopup.Debounce);

        return new ReduceResult(next, [effect]);
    }

    // The rows of an empty buffer: the applied labels of a labels field, or the quick rows of a person field
    // (PersonField: no lookup, nothing highlighted, so Enter still saves the form as it stands). Typing replaces them
    // with the single search path. Null for any other field, or when no row applies.
    private static SuggestionState? QuickPopup(AppState state, FormState form)
    {
        if (LabelRows(form) is { } labels)
        {
            return labels;
        }

        return PersonField.QuickPopup(state, form);
    }

    // A labels field lists the labels it has while the buffer is empty, in every form, so one can be taken off; null
    // for any other field or when there is none.
    private static SuggestionState? LabelRows(FormState form)
    {
        if (form.Focus >= form.Fields.Count || form.Navigating || form.IsSaving)
        {
            return null;
        }

        var field = form.Fields[form.Focus];

        if (field.Kind != FormFieldKind.Labels || !field.IsEditable || field.Value.Chosen.Count == 0)
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

        if (field.Kind == FormFieldKind.User)
        {
            return PersonField.Source(form.Target, field);
        }

        var target = form.Target;

        if (field.Lookup is { } lookup && FormFieldKinds.IsChoice(field.Kind) && target.SearchProjectKey is { } project)
        {
            return new SuggestionSource(lookup, null, [], project);
        }

        if (field.Kind != FormFieldKind.Parent || (target.IssueKey is null && target.ParentProjectKey is null))
        {
            return null;
        }

        // A parent is searched in the project of the issue (a link target may be in any project, so it names none).
        return new SuggestionSource(
            SuggestionKind.Issues,
            target.IssueKey,
            [],
            target.ParentProjectKey,
            target.ParentHierarchyLevel);
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
