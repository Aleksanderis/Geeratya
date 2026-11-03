using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
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
        ActionIds.IssueLink,
        ActionIds.IssueUnlink,
    ];

    // The first word of the typed commands that write; the palette rows go through the action ids above.
    private static readonly HashSet<string> _writeCommands =
    [
        PaletteCommands.Status,
        PaletteCommands.Labels,
        PaletteCommands.Create,
        PaletteCommands.Edit,
        PaletteCommands.Priority,
        PaletteCommands.Estimate,
        PaletteCommands.Assign,
        PaletteCommands.AssignMe,
    ];

    /// <summary>True when <paramref name="action"/> is a write on the selected issue or list.</summary>
    public static bool IsWrite(string action) => _writes.Contains(action);

    /// <summary>True when the typed command (<c>:status</c>, ...) writes.</summary>
    public static bool IsWriteCommand(string commandLine) => _writeCommands.Contains(commandLine.Split(' ', 2)[0]);

    /// <summary>True when <paramref name="action"/> writes and the active tab shows a closed sprint.</summary>
    public static bool Refuses(AppState state, string action)
    {
        return _writes.Contains(action) && state.Mode == InputMode.Normal && ShowsClosedSprint(state);
    }

    /// <summary>True when the typed command (<c>:status</c>, <c>:assign me</c>, ...) writes and the tab shows a closed sprint.</summary>
    public static bool RefusesCommand(AppState state, string commandLine)
    {
        var word = commandLine.Split(' ', 2)[0];

        return _writeCommands.Contains(word) && ShowsClosedSprint(state);
    }

    private static bool ShowsClosedSprint(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return tab is { Definition.Source: ViewSourceKind.Sprint, Sprint.State: SprintState.Closed };
    }
}
