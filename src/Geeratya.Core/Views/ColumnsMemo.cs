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
}
