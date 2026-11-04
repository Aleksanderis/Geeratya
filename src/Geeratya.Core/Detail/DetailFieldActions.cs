using Geeratya.Core.Forms;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;

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
        return fieldId switch
        {
            DetailFieldIds.Status => TransitionForms.OpenPicker(state),
            DetailFieldIds.Assignee => FormOpener.Assign(state),
            DetailFieldIds.Estimate => FormOpener.Estimate(state),
            DetailFieldIds.Sprint => SprintMoveActions.OpenPicker(state),
            DetailFieldIds.Parent or DetailFieldIds.Epic => DetailActions.OpenParent(state),
            DetailFieldIds.Summary or DetailFieldIds.Priority or DetailFieldIds.Labels or DetailFieldIds.Description
                => FormOpener.EditField(state, fieldId),
            _ => Say(state, $"{Title(fieldId)} is read-only here."),
        };
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
