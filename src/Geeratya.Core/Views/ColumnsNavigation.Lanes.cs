using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

public static partial class ColumnsNavigation
{
    private static readonly BoardCursor _noCursor = new(0, [], [], 0);

    /// <summary>The key of the lane whose columns hold the cursor; empty without a grouping.</summary>
    public static string CurrentLaneKey(TabState tab)
    {
        return Swimlanes.Current(LanesOf(tab), tab.Lane)?.Key ?? string.Empty;
    }

    /// <summary>
    /// The columns of lane <paramref name="laneKey"/>: the current lane's through the tab's memo, another lane's
    /// (stacked lanes, #891) from a per-lane memo that is dropped when the rows change.
    /// </summary>
    public static IReadOnlyList<BoardColumnGroup> ColumnsOfLane(TabState tab, string laneKey)
    {
        return laneKey == CurrentLaneKey(tab) ? ColumnsOf(tab) : tab.Columns.GetLaneColumns(tab, laneKey, BuildLaneColumns);
    }

    /// <summary>
    /// The cursor to draw lane <paramref name="region"/> with in a body of <paramref name="viewWidth"/> cells. The
    /// current lane's is the tab's; another lane keeps its own selected cards and scrolls (<see cref="TabState.LaneCursors"/>)
    /// but follows the current lane's column, so every lane shows the same window of columns.
    /// </summary>
    public static BoardCursor CursorOfLane(
        TabState tab,
        LaneRegion region,
        IReadOnlyList<BoardColumnGroup> columns,
        int viewWidth)
    {
        var view = new BoardViewport(region.BoardRows, viewWidth);

        if (region.Key == CurrentLaneKey(tab))
        {
            return BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, view);
        }

        var seed = tab.LaneCursors.GetValueOrDefault(region.Key) ?? _noCursor;

        return BoardMotion.Resolve(columns, seed with { Column = tab.Cursor?.Column ?? 0 }, null, view);
    }

    /// <summary>
    /// Makes lane <paramref name="laneKey"/> the current one. With stacked lanes the lane keeps its own cursor (the
    /// one it had when it was left) and the column stays; otherwise the cursor goes to the first card of the lane.
    /// The marks are dropped either way, as they would stay on cards of another lane.
    /// </summary>
    public static TabState SwitchLane(TabState tab, string laneKey)
    {
        var current = CurrentLaneKey(tab);

        if (laneKey == current)
        {
            return tab;
        }

        if (!LaneRegions.IsStacked(tab))
        {
            return Resync(tab with { Lane = laneKey });
        }

        var saved = new Dictionary<string, BoardCursor>(tab.LaneCursors);
        var keys = new Dictionary<string, string>(tab.LaneKeys);

        if (tab.Cursor is not null)
        {
            saved[current] = tab.Cursor;
        }

        if (SelectedIssue(tab) is { } selected)
        {
            keys[current] = selected.Key;
        }

        var next = tab with
        {
            Lane = laneKey,
            LaneCursors = saved,
            LaneKeys = keys,
            Cursor = null,
            Marks = [],
            VisualAnchor = null,
        };
        var columns = ColumnsOf(next);
        var seed = saved.GetValueOrDefault(laneKey) ?? _noCursor;
        var column = tab.Cursor?.Column ?? 0;
        var cursor = BoardMotion.Resolve(columns, seed with { Column = column }, keys.GetValueOrDefault(laneKey), ViewOf(next));

        return Follow(next, columns, cursor);
    }

    /// <summary>
    /// Settles the cursor for the room the tab's board has now (stacking was switched, so the current lane's height
    /// changed) and forgets the cursors kept for the other lanes.
    /// </summary>
    public static TabState Refit(TabState tab)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        return Follow(tab with { LaneCursors = TabState.EmptyLaneCursors, LaneKeys = TabState.EmptyLaneKeys }, columns, cursor);
    }

    /// <summary>
    /// The key of the drawn lane that lists the card <paramref name="key"/> (stacked lanes only), null when the
    /// lanes are not stacked or no drawn lane has it.
    /// </summary>
    private static string? LaneShowing(TabState tab, string key)
    {
        if (!LaneRegions.IsStacked(tab))
        {
            return null;
        }

        var current = CurrentLaneKey(tab);

        if (ColumnsOfLane(tab, current).Any(column => HasKey(column.Rows, key)))
        {
            return current;
        }

        foreach (var region in LaneRegions.Regions(tab))
        {
            if (ColumnsOfLane(tab, region.Key).Any(column => HasKey(column.Rows, key)))
            {
                return region.Key;
            }
        }

        return null;
    }

    private static bool HasKey(IReadOnlyList<IssueSummary> rows, string key)
    {
        return IndexOfKey(rows, key) >= 0;
    }
}
