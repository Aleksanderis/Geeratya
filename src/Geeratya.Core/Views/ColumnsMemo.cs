using Geeratya.Core.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// The columns last built for a tab, kept so one key and the frame it draws group the rows once instead of once per
/// question asked of the columns (#758). The inputs are compared by reference (the lists) or value (the settings), so
/// a changed row list, board, lane, folded card or cut-off always rebuilds; the memo owns no invalidation rule of its
/// own and holds one entry, dropped with the tab. Copies of a <see cref="TabState"/> share it, and two tabs never do.
/// </summary>
public sealed class ColumnsMemo
{
    private Entry? _last;
    private LaneEntry? _lanes;

    /// <summary>The columns for these inputs, built by <paramref name="build"/> only when they differ from the last call.</summary>
    internal IReadOnlyList<BoardColumnGroup> Get(TabState tab, Func<TabState, IReadOnlyList<BoardColumnGroup>> build)
    {
        var last = _last;

        if (last is not null
            && ReferenceEquals(last.Rows, tab.Rows)
            && ReferenceEquals(last.Board, tab.Board)
            && ReferenceEquals(last.Expanded, tab.ExpandedSubtasks)
            && last.Swimlane == tab.Swimlane
            && last.Lane == tab.Lane
            && last.DoneCutoffDays == tab.DoneCutoffDays)
        {
            return last.Columns;
        }

        var columns = build(tab);

        _last = new Entry(tab.Rows, tab.Board, tab.ExpandedSubtasks, tab.Swimlane, tab.Lane, tab.DoneCutoffDays, columns);

        return columns;
    }

    /// <summary>
    /// The lanes of the tab's rows for its grouping, built only when the row list or the grouping changed, so the lane
    /// strip does not pass over every row on each frame (#874).
    /// </summary>
    internal IReadOnlyList<SwimlaneLane> GetLanes(TabState tab)
    {
        var last = _lanes;

        if (last is not null && ReferenceEquals(last.Rows, tab.Rows) && last.Swimlane == tab.Swimlane)
        {
            return last.Lanes;
        }

        var lanes = Swimlanes.Lanes(tab.Swimlane, tab.Rows);

        _lanes = new LaneEntry(tab.Rows, tab.Swimlane, lanes);

        return lanes;
    }

    /// <summary>The memo is a cache, not state: two tabs that differ only in it are equal.</summary>
    public override bool Equals(object? obj)
    {
        return obj is ColumnsMemo;
    }

    /// <summary>See <see cref="Equals(object?)"/>.</summary>
    public override int GetHashCode()
    {
        return 0;
    }

    private sealed record Entry(
        IReadOnlyList<IssueSummary> Rows,
        BoardConfiguration? Board,
        IReadOnlyList<string> Expanded,
        SwimlaneKind Swimlane,
        string? Lane,
        int DoneCutoffDays,
        IReadOnlyList<BoardColumnGroup> Columns);

    private sealed record LaneEntry(
        IReadOnlyList<IssueSummary> Rows,
        SwimlaneKind Swimlane,
        IReadOnlyList<SwimlaneLane> Lanes);
}
