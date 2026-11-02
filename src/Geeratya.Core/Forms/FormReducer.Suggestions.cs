using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// The suggestion popup of the form (<c>docs/ux-workflows.md</c>, "Suggestions"): it opens by itself when text is
/// typed into a labels, user or parent field, follows the typing buffer, and closes on Esc, on accepting a row, or
/// when the focus leaves the field.
/// </summary>
public static partial class FormReducer
{
    private const int KnownLabelLimit = 500;

    // The keys of an open popup: Esc closes it alone, Up and Down move, Tab and Enter take the highlighted row. Any
    // other key is not the popup's (null) and goes on to the field.
    private static ReduceResult? Suggesting(
        AppState state,
        TabState tab,
        FormState form,
        SuggestionState popup,
        string? action)
    {
        return action switch
        {
            ActionIds.Cancel => Result(Replace(state, tab, form with { Suggest = null }), tab.Id),
            ActionIds.PickerNext => Moved(state, tab, form, popup, 1),
            ActionIds.PickerPrevious => Moved(state, tab, form, popup, -1),
            ActionIds.FormNext or ActionIds.FormFieldNext when popup.Selected is { } picked
                => Result(Replace(state, tab, Accepted(form, picked)), tab.Id),
            _ => null,
        };
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

        return new ReduceResult(closed, [.. result.Effects, new CancelSuggestionsEffect(tabId)]);
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

    private static SuggestionSource? SourceOf(TabState tab, FormState form, FormField field)
    {
        if (field.Kind == FormFieldKind.Labels)
        {
            var known = tab.Rows.SelectMany(row => row.Labels).Distinct(StringComparer.Ordinal).Take(KnownLabelLimit);

            return new SuggestionSource(SuggestionKind.Labels, null, [.. known]);
        }

        if (form.Target.IssueKey is not { } key)
        {
            return null;
        }

        return field.Kind switch
        {
            FormFieldKind.User => new SuggestionSource(UserKind(field), key, []),
            FormFieldKind.Parent => new SuggestionSource(SuggestionKind.Issues, key, []),
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
