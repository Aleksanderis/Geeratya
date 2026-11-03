using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Sprints;

/// <summary>
/// <c>sprint.goal</c>: the full goal of the sprint a Sprint view shows, in the notice flavour of the Confirm dialog
/// (the header line only cuts it to the width). Reads only; the dialog closes on <c>y</c>, <c>n</c>, <c>Esc</c> or a click.
/// </summary>
public static class SprintGoalNotice
{
    public const string Title = "Sprint goal";
    public const string NoSprintText = "No sprint shown in this tab.";
    public const string NoGoalText = "This sprint has no goal.";

    /// <summary>Opens the notice for the active tab's sprint, or answers with the reason there is none.</summary>
    public static AppState Open(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        if (tab?.Sprint is not { } sprint)
        {
            return state with { Message = NoSprintText };
        }

        if (string.IsNullOrWhiteSpace(sprint.Goal))
        {
            return state with { Message = NoGoalText };
        }

        var request = new ConfirmRequest(
            $"{sprint.Name}\n\n{sprint.Goal.Trim()}",
            ActionIds.Cancel,
            Label: "Close",
            Title: Title);

        return state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty, Message = null };
    }
}
