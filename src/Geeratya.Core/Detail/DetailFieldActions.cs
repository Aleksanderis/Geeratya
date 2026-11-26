using Geeratya.Core.Forms;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// Enter on a field of the issue detail: opens the field for edit through the write path that already exists for it
/// (transition picker, people picker, edit form limited to the field, estimate form, sprint picker). The parent or
/// epic row opens the parent chooser; the parent issue itself is <c>issue.open-parent</c>. Fields Jira does not let
/// this screen change say so in the status line. Pure.
/// </summary>
internal static class DetailFieldActions
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// The key or command of one field (<c>issue.assign</c>, <c>:priority</c>, <c>:estimate</c>, <c>:labels</c>,
    /// <c>issue.transition</c>) opens it the way Enter does in Details: from a list or board the detail overlay
    /// opens on the selected issue first, so a field edits the same way from every route.
    /// </summary>
    public static ReduceResult OpenField(AppState state, string fieldId)
    {
        var tab = ActiveTab(state);

        if (tab is null || tab.Detail.Count > 0 || state.Session.ProfileName is null)
        {
            return Open(state, fieldId);
        }

        // A card of the columns layout edits in place: the picker or form opens over the board for the selected card,
        // the same way the sub-task keys do, and no issue view opens first (#1063).
        if (tab.Definition.Layout == ViewLayout.Columns && InPlace(fieldId))
        {
            return Open(state, fieldId, false);
        }

        var effects = new List<Effect>();
        var opened = DetailReducer.OpenSelected(state, effects);

        if (ActiveTab(opened) is not { Detail.Count: > 0 })
        {
            return Open(state, fieldId);
        }

        var field = Open(opened, fieldId);

        return new ReduceResult(field.State, [.. effects, .. field.Effects]);
    }

    public static ReduceResult Open(AppState state, string fieldId)
    {
        return Open(state, fieldId, true);
    }

    // Fields whose openers work from the selected card without an opened issue.
    private static bool InPlace(string fieldId)
    {
        return fieldId is DetailFieldIds.Status or DetailFieldIds.Assignee or DetailFieldIds.Estimate
            or DetailFieldIds.Priority or DetailFieldIds.Labels or DetailFieldIds.Reporter or DetailFieldIds.DueDate
            or DetailFieldIds.Parent or DetailFieldIds.Epic;
    }

    // hung: the form hangs under its field in the issue view; without an issue view it is the plain form.
    private static ReduceResult Open(AppState state, string fieldId, bool hung)
    {
        if (Refused(state, fieldId))
        {
            return Say(state, $"{Title(fieldId)} cannot be edited on this issue.");
        }

        return fieldId switch
        {
            DetailFieldIds.Status => Dropdown(TransitionForms.OpenPicker(state), fieldId, hung),
            DetailFieldIds.Assignee => Dropdown(FormOpener.Assign(state), fieldId, hung),
            DetailFieldIds.Estimate => Dropdown(FormOpener.Estimate(state), fieldId, hung),
            DetailFieldIds.Sprint => Dropdown(SprintMoveActions.OpenField(state), fieldId, hung),
            DetailFieldIds.Parent or DetailFieldIds.Epic
                => Dropdown(FormOpener.EditField(state, FormFieldIds.Parent), fieldId, hung),
            DetailFieldIds.Priority or DetailFieldIds.Labels or DetailFieldIds.Reporter or DetailFieldIds.DueDate
                => Dropdown(FormOpener.EditField(state, fieldId), fieldId, hung),
            DetailFieldIds.Summary or DetailFieldIds.Description => FormOpener.EditInPane(state, fieldId),
            _ => Say(state, $"{Title(fieldId)} is read-only here."),
        };
    }

    // The edit metadata is known and does not list the field: nothing opens. Unknown metadata lets the form decide.
    private static bool Refused(AppState state, string fieldId)
    {
        if (fieldId is not (DetailFieldIds.Priority or DetailFieldIds.Assignee or DetailFieldIds.Reporter
            or DetailFieldIds.Labels or DetailFieldIds.Parent or DetailFieldIds.Epic or DetailFieldIds.DueDate))
        {
            return false;
        }

        fieldId = DetailsPane.EditId(fieldId);

        var tab = ActiveTab(state);

        return tab is { Detail.Count: > 0 } && tab.Detail[^1].EditableFields is { } known && !known.Contains(fieldId);
    }

    // The opened form hangs under the field as a dropdown instead of being drawn as a box of its own.
    private static ReduceResult Dropdown(ReduceResult opened, string fieldId, bool hung = true)
    {
        if (!hung)
        {
            return opened;
        }

        var tab = ActiveTab(opened.State);

        if (tab?.Form is not { } form)
        {
            return opened;
        }

        var under = tab with { Form = form with { Dropdown = fieldId }, Detail = AtField(tab.Detail, fieldId) };

        return new ReduceResult(ViewReducer.Replace(opened.State, under), opened.Effects);
    }

    // The dropdown hangs under the cursor field: a field opened by command (:priority, a list key) takes the cursor
    // first, so it opens where the same field opens from Enter on it.
    private static IReadOnlyList<DetailState> AtField(IReadOnlyList<DetailState> stack, string fieldId)
    {
        if (stack.Count == 0 || stack[^1] is not { Document.Side: { } side } top)
        {
            return stack;
        }

        for (var line = 0; line < side.Targets.Count; line++)
        {
            if (side.Targets[line] is { Kind: DetailTargetKind.Field } target && target.Value == fieldId)
            {
                return [.. stack.Take(stack.Count - 1), top with { SideCursor = side.Snap(line) }];
            }
        }

        return stack;
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }

    private static string Title(string fieldId)
    {
        return fieldId.Length == 0 ? fieldId : $"{char.ToUpperInvariant(fieldId[0])}{fieldId[1..]}";
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
