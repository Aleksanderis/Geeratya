using Geeratya.Core.Forms;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// One workflow transition the user can pick, reduced to what the state layer needs. The id is only valid for the
/// issue and moment it was read (ids are per project); a request carries it from the pick to the write.
/// </summary>
/// <param name="Id">The transition id to post.</param>
/// <param name="Name">The transition name.</param>
/// <param name="Target">The status the issue ends in.</param>
/// <param name="NeedsForm">The transition asks for fields: a required one without default, or a screen with some.</param>
/// <param name="AsksFirst">
/// The move is confirmed before it is sent: the target is in the done category, or the workflow shows a screen that
/// has nothing to fill in. Jira does not expose post functions, so these are the only hints of a side effect.
/// </param>
public sealed record TransitionChoice(string Id, string Name, IssueStatus Target, bool NeedsForm, bool AsksFirst)
{
    private const string DoneCategory = "done";

    /// <summary>How the transition reads in a list: the target status, with the name when it differs.</summary>
    public string Label =>
        Name.Length == 0 || string.Equals(Name, Target.Name, StringComparison.OrdinalIgnoreCase)
            ? Target.Name
            : $"{Name} → {Target.Name}";

    public static TransitionChoice Of(Transition transition)
    {
        var needsForm = transition.RequiredFields.Any() || (transition.HasScreen && HasEditableField(transition));
        var done = string.Equals(transition.Target.CategoryKey, DoneCategory, StringComparison.Ordinal);

        return new TransitionChoice(
            transition.Id,
            transition.Name,
            transition.Target,
            needsForm,
            !needsForm && (done || transition.HasScreen));
    }

    // A screen of fields the form cannot edit (the system comment, say) has nothing to ask the user.
    private static bool HasEditableField(Transition transition)
    {
        return FormFieldMapper.FromTransition(transition.Fields).Any(field => field.IsEditable);
    }
}
