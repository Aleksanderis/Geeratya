using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Detail;

/// <summary>
/// While an issue detail is open, the view underneath is inactive: actions that move or change the board, list or
/// backlog (columns, layout, filters, sections, rank, row jump, sprint lifecycle) do nothing, so a key never acts
/// on a view the user sees as inactive. Keys that belong to the detail (panes, rows, issue writes) pass.
/// </summary>
internal static class DetailUnderlayGate
{
    private static readonly HashSet<string> _underlayOnly =
    [
        ActionIds.NavigateLeft,
        ActionIds.NavigateRight,
        ActionIds.NavigateFirstColumn,
        ActionIds.NavigateLastColumn,
        ActionIds.ViewLayoutToggle,
        ActionIds.ViewQuickFilter,
        ActionIds.ViewSwimlane,
        ActionIds.ViewDoneCutoff,
        ActionIds.SectionNext,
        ActionIds.SectionPrevious,
        ActionIds.RankUp,
        ActionIds.RankDown,
        ActionIds.RankTop,
        ActionIds.RankBottom,
        ActionIds.RankTo,
        ActionIds.RowJump,
        ActionIds.SprintSwitch,
        ActionIds.SprintCreate,
        ActionIds.SprintEdit,
        ActionIds.SprintStart,
        ActionIds.SprintComplete,
    ];

    /// <summary>True when <paramref name="action"/> only acts on the view and an issue detail covers it.</summary>
    public static bool Refuses(AppState state, string action)
    {
        if (!_underlayOnly.Contains(action) || state.Mode != InputMode.Normal)
        {
            return false;
        }

        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return tab is { Detail.Count: > 0 };
    }
}
