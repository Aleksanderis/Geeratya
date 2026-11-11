using Geeratya.Core.Backlog;
using Geeratya.Core.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

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
    private LaneColumnsEntry? _laneColumns;
    private BacklogEntry? _backlog;
    private RowFlagsEntry? _flags;
    private KeyIndexEntry? _keyIndex;

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

    /// <summary>
    /// The columns of one lane (stacked lanes, #891), built by <paramref name="build"/> once per lane until the rows,
    /// board, folded cards, grouping or cut-off change. Every lane's entry is dropped together.
    /// </summary>
    internal IReadOnlyList<BoardColumnGroup> GetLaneColumns(
        TabState tab,
        string laneKey,
        Func<TabState, string, IReadOnlyList<BoardColumnGroup>> build)
    {
        var last = _laneColumns;

        if (last is null
            || !ReferenceEquals(last.Rows, tab.Rows)
            || !ReferenceEquals(last.Board, tab.Board)
            || !ReferenceEquals(last.Expanded, tab.ExpandedSubtasks)
            || last.Swimlane != tab.Swimlane
            || last.DoneCutoffDays != tab.DoneCutoffDays)
        {
            last = new LaneColumnsEntry(tab.Rows, tab.Board, tab.ExpandedSubtasks, tab.Swimlane, tab.DoneCutoffDays, []);
            _laneColumns = last;
        }

        if (!last.ByLane.TryGetValue(laneKey, out var columns))
        {
            columns = build(tab, laneKey);
            last.ByLane[laneKey] = columns;
        }

        return columns;
    }

    /// <summary>
    /// The backlog layout for these inputs, built by <paramref name="build"/> only when the row list, the sprint list
    /// or the collapsed sections changed (#930), so a key and its frame share one pass over the rows.
    /// </summary>
    internal BacklogLayout GetBacklog(TabState tab, Func<TabState, BacklogLayout> build)
    {
        var last = _backlog;

        if (last is not null
            && ReferenceEquals(last.Rows, tab.Rows)
            && ReferenceEquals(last.Sprints, tab.Sprints)
            && ReferenceEquals(last.Collapsed, tab.CollapsedSections))
        {
            return last.Layout;
        }

        var layout = build(tab);

        _backlog = new BacklogEntry(tab.Rows, tab.Sprints, tab.CollapsedSections, layout);

        return layout;
    }

    /// <summary>Whether any row has an estimate or an epic, computed once per row list (#930).</summary>
    internal RowFlags GetFlags(TabState tab)
    {
        var last = _flags;

        if (last is not null && ReferenceEquals(last.Rows, tab.Rows))
        {
            return last.Flags;
        }

        var estimate = false;
        var epic = false;

        for (var index = 0; index < tab.Rows.Count && !(estimate && epic); index++)
        {
            estimate |= tab.Rows[index].Estimate is not null;
            epic |= tab.Rows[index].Epic is not null;
        }

        var flags = new RowFlags(estimate, epic);

        _flags = new RowFlagsEntry(tab.Rows, flags);

        return flags;
    }

    /// <summary>
    /// The index of the first row with this key (ignoring case), -1 when none: one dictionary per row list instead of a
    /// scan per Visual key or card move (#930).
    /// </summary>
    internal int IndexOfKey(TabState tab, string key)
    {
        var last = _keyIndex;

        if (last is null || !ReferenceEquals(last.Rows, tab.Rows))
        {
            var indexes = new Dictionary<string, int>(tab.Rows.Count, StringComparer.OrdinalIgnoreCase);

            for (var index = 0; index < tab.Rows.Count; index++)
            {
                indexes.TryAdd(tab.Rows[index].Key, index);
            }

            last = new KeyIndexEntry(tab.Rows, indexes);
            _keyIndex = last;
        }

        return last.Indexes.GetValueOrDefault(key, -1);
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

    private sealed record LaneColumnsEntry(
        IReadOnlyList<IssueSummary> Rows,
        BoardConfiguration? Board,
        IReadOnlyList<string> Expanded,
        SwimlaneKind Swimlane,
        int DoneCutoffDays,
        Dictionary<string, IReadOnlyList<BoardColumnGroup>> ByLane);

    private sealed record BacklogEntry(
        IReadOnlyList<IssueSummary> Rows,
        IReadOnlyList<Sprint> Sprints,
        IReadOnlyList<string> Collapsed,
        BacklogLayout Layout);

    private sealed record RowFlagsEntry(IReadOnlyList<IssueSummary> Rows, RowFlags Flags);

    private sealed record KeyIndexEntry(IReadOnlyList<IssueSummary> Rows, Dictionary<string, int> Indexes);
}
