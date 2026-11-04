using Geeratya.Core.Forms;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// Enter on a field of the issue detail: opens the field for edit through the write path that already exists for it
/// (transition picker, people picker, edit form limited to the field, estimate form, sprint picker). Fields Jira
/// does not let this screen change say so in the status line. Pure.
/// </summary>
internal static class DetailFieldActions
{
    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Open(AppState state, string fieldId)
    {
        if (Refused(state, fieldId))
        {
            return Say(state, $"{Title(fieldId)} cannot be edited on this issue.");
        }

        return fieldId switch
        {
            DetailFieldIds.Status => TransitionForms.OpenPicker(state),
            DetailFieldIds.Assignee => Dropdown(FormOpener.Assign(state), fieldId),
            DetailFieldIds.Estimate => FormOpener.Estimate(state),
            DetailFieldIds.Sprint => SprintMoveActions.OpenPicker(state),
            DetailFieldIds.Parent or DetailFieldIds.Epic => DetailActions.OpenParent(state),
            DetailFieldIds.Priority or DetailFieldIds.Labels
                => Dropdown(FormOpener.EditField(state, fieldId), fieldId),
            DetailFieldIds.Summary or DetailFieldIds.Description => FormOpener.EditInPane(state, fieldId),
            _ => Say(state, $"{Title(fieldId)} is read-only here."),
        };
    }

    // The edit metadata is known and does not list the field: nothing opens. Unknown metadata lets the form decide.
    private static bool Refused(AppState state, string fieldId)
    {
        if (fieldId is not (DetailFieldIds.Priority or DetailFieldIds.Assignee or DetailFieldIds.Labels))
        {
            return false;
        }

        var tab = ActiveTab(state);

        return tab is { Detail.Count: > 0 } && tab.Detail[^1].EditableFields is { } known && !known.Contains(fieldId);
    }

    // The opened form hangs under the field as a dropdown instead of being drawn as a box of its own.
    private static ReduceResult Dropdown(ReduceResult opened, string fieldId)
    {
        var tab = ActiveTab(opened.State);

        if (tab?.Form is not { } form)
        {
            return opened;
        }

        return new ReduceResult(
            ViewReducer.Replace(opened.State, tab with { Form = form with { Dropdown = fieldId } }),
            opened.Effects);
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
