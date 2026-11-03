using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// Closed sprints are read only: a write action on the active tab while it shows one is refused with a note, so the
/// keys, the palette and the menus behave alike (they all end in an action id).
/// </summary>
public static class ClosedSprintGate
{
    public const string Refusal = "Closed sprint: read only.";

    private static readonly HashSet<string> _writes =
    [
        ActionIds.IssueTransition,
        ActionIds.IssueTransitionNext,
        ActionIds.IssueTransitionPrevious,
        ActionIds.IssueEdit,
        ActionIds.IssueAssign,
        ActionIds.IssueAssignMe,
        ActionIds.IssueDelete,
        ActionIds.IssueCreate,
        ActionIds.IssueCreateSubtask,
        ActionIds.RankUp,
        ActionIds.RankDown,
        ActionIds.RankTop,
        ActionIds.RankBottom,
        ActionIds.RankTo,
    ];

    /// <summary>True when <paramref name="action"/> writes and the active tab shows a closed sprint.</summary>
    public static bool Refuses(AppState state, string action)
    {
        if (!_writes.Contains(action) || state.Mode != InputMode.Normal)
        {
            return false;
        }

        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return tab is { Definition.Source: ViewSourceKind.Sprint, Sprint.State: SprintState.Closed };
    }
}
