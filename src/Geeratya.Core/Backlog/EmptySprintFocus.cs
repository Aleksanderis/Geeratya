using Geeratya.Core.Keymap;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;

namespace Geeratya.Core.Backlog;

/// <summary>
/// While the cursor of a Scrum backlog stands on the header of an empty sprint (<c>TabState.FocusedSprintId</c>) no
/// issue is under it, yet the selected row is still the one after the section. Every action that would write to or open
/// that row is refused with a note, so a key never acts on an issue the cursor is not on.
/// </summary>
public static class EmptySprintFocus
{
    public const string Refusal = "The cursor is on an empty sprint: move onto an issue first.";

    private static readonly HashSet<string> _rowActions =
    [
        ActionIds.ItemOpen,
        ActionIds.IssueOpenInBrowser,
        ActionIds.SelectionVisual,
        ActionIds.SelectionMark,
        ActionIds.SectionToggle,
        ActionIds.SectionNext,
        ActionIds.SectionPrevious,
    ];

    /// <summary>True when the cursor is on an empty sprint and <paramref name="action"/> targets a row.</summary>
    public static bool Refuses(AppState state, string action)
    {
        return state.Mode == InputMode.Normal
            && (_rowActions.Contains(action) || ClosedSprintGate.IsWrite(action))
            && IsFocused(state);
    }

    /// <summary>The same for a typed command that writes.</summary>
    public static bool RefusesCommand(AppState state, string commandLine)
    {
        return ClosedSprintGate.IsWriteCommand(commandLine) && IsFocused(state);
    }

    private static bool IsFocused(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return tab is { FocusedSprintId: not null, Detail.Count: 0 };
    }
}
