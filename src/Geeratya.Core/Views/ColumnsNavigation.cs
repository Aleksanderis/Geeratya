using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// Moves the selection of a tab in the columns layout. Pure on a <see cref="TabState"/>: the cursor says which card
/// is selected, and the tab's own selected row and key follow it so the list layout, a refresh and the issue detail
/// all see the same issue.
/// </summary>
public static partial class ColumnsNavigation
{
    /// <summary>Moves inside the selected column.</summary>
    public static TabState Move(TabState tab, ListMove move)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.MoveCard(columns, tab.Cursor, tab.SelectedKey, move, ViewOf(tab));

        return Follow(tab, columns, cursor);
    }

    /// <summary>
    /// A click: places the cursor on the card or column header at a body cell drawn for <paramref name="width"/>.
    /// With stacked lanes (#891) a click in another lane, on a card or its header row, makes that lane the current one
    /// first (a folded lane opens); a click on the chevron of a lane header folds or opens it (#893). A cell with neither is returned unchanged.
    /// </summary>
    public static TabState Place(TabState tab, int width, int column, int row)
    {
        if (LaneRegions.RegionAtRow(tab, row) is not { } region)
        {
            return tab;
        }

        if (region.HasHeader && row == region.Top)
        {
            return column < LaneRegions.ChevronCells
                ? LaneFolds.ToggleAt(tab, region.Key)
                : SwitchLane(tab, region.Key);
        }

        if (region.Folded)
        {
            return tab;
        }

        var shown = SwitchLane(tab, region.Key);

        var columns = ColumnsOf(shown);
        var cursor = BoardMotion.Resolve(columns, shown.Cursor, shown.SelectedKey, ViewOf(shown));

        if (HitAt(shown, columns, cursor, width, column, row) is not { } hit)
        {
            return shown;
        }

        return Follow(shown, columns, BoardMotion.Place(columns, shown.Cursor, shown.SelectedKey, hit, ViewOf(shown)));
    }

    /// <summary>True when the body row is the header row of a stacked lane (#893).</summary>
    public static bool IsLaneHeaderAt(TabState tab, int row)
    {
        return LaneRegions.RegionAtRow(tab, row) is { HasHeader: true } region && region.Top == row;
    }

    /// <summary>True when the body cell is on a card (not a column header or an empty cell), in any drawn lane.</summary>
    public static bool IsCardAt(TabState tab, int width, int column, int row)
    {
        if (LaneRegions.RegionAtRow(tab, row) is not { Folded: false } region)
        {
            return false;
        }

        var columns = ColumnsOfLane(tab, region.Key);
        var cursor = CursorOfLane(tab, region, columns, width);
        var hit = BoardHits.At(columns, cursor, region.BoardRows, width, column, row - region.BoardTop, tab.Next is not null);

        return hit is { Card: not null };
    }

    /// <summary>
    /// True when the body cell is on the bottom row of the selected card that lists sub-tasks (<c>▸1/3</c>, #558,
    /// #706): the key row of the card, wherever its height puts it.
    /// A click on an unselected card only selects it, so clicking its key never folds it. One-line cards have no
    /// such row.
    /// </summary>
    public static bool IsSubtaskRowAt(TabState tab, int width, int column, int row)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        var region = LaneRegions.FocusRegion(tab);

        if (!region.Contains(row)
            || BoardCards.IsCompact(region.BoardRows, ListLayout.HeaderRows)
            || BoardHits.Locate(columns, cursor, region.BoardRows, width, column, row - region.BoardTop)
                is not { Row.Kind: CardRowKind.Key, Hit: { Card: { } card } hit }
            || columns[hit.Column].Rows[card] is not { IsListed: false, Subtasks.Count: > 0 } target)
        {
            return false;
        }

        return string.Equals(target.Key, tab.SelectedKey, StringComparison.Ordinal);
    }

    /// <summary>
    /// A drag: how many columns the pointer moved from the card at the first cell to the column under the second
    /// (positive to the right), or null when the drag did not start on a card, ended outside the columns, or the
    /// layout shows one column at a time (nothing to drop on).
    /// </summary>
    public static int? DragSteps(TabState tab, int width, int fromColumn, int fromRow, int toColumn)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        if (BoardWindows.For(columns.Count, cursor.Column, width).Count < 2
            || HitAt(tab, columns, cursor, width, fromColumn, fromRow) is not { Card: not null } source
            || BoardHits.ColumnAtCell(columns, cursor, width, toColumn) is not { } target)
        {
            return null;
        }

        return target - source.Column;
    }

    /// <summary>Moves to another column.</summary>
    public static TabState MoveColumn(TabState tab, BoardColumnMove move)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.MoveColumn(columns, tab.Cursor, tab.SelectedKey, move, ViewOf(tab));

        return Follow(tab, columns, cursor);
    }

    /// <summary>
    /// Moves the cursor to the card with <paramref name="key"/> after its status changed, keeping the scroll offsets
    /// of the columns. A tab without a cursor, or without that card, is returned as it is.
    /// </summary>
    public static TabState FollowCard(TabState tab, string key)
    {
        return tab.Cursor is null ? tab : Select(tab, key);
    }

    /// <summary>
    /// Puts the cursor on the card with <paramref name="key"/> in whatever column it is, keeping the scroll of the
    /// columns (a flash jump; <paramref name="laneKey"/> names the lane of the label picked, for a card in several lanes). A tab without that card is returned with its cursor settled.
    /// </summary>
    public static TabState Select(TabState tab, string key, string? laneKey = null)
    {
        // A card listed in several lanes (grouping by labels): the lane the flash label was drawn in wins.
        var lane = laneKey is not null && LaneRegions.IsStacked(tab) ? laneKey : LaneShowing(tab, key);

        tab = SwitchLane(tab, lane ?? CurrentLaneKey(tab));

        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        return Follow(tab, columns, BoardMotion.Follow(columns, cursor, key, ViewOf(tab)));
    }

    /// <summary>The issue under the cursor, null when the selected column is empty or the tab has no rows.</summary>
    public static IssueSummary? SelectedIssue(TabState tab)
    {
        var columns = ColumnsOf(tab);

        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        return BoardMotion.SelectedIssue(columns, cursor);
    }

    /// <summary>The cards of the selected column and the selected one, null when the tab has no columns.</summary>
    public static ColumnSelection? SelectedColumn(TabState tab)
    {
        var columns = ColumnsOf(tab);

        if (columns.Count == 0)
        {
            return null;
        }

        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        return new ColumnSelection(
            columns[cursor.Column].Rows,
            cursor.Card,
            columns[cursor.Column].Nested);
    }

    /// <summary>The columns the layout draws for the tab's rows.</summary>
    public static IReadOnlyList<BoardColumnGroup> ColumnsOf(TabState tab)
    {
        return tab.Columns.Get(tab, BuildColumns);
    }

    private static IReadOnlyList<BoardColumnGroup> BuildColumns(TabState tab)
    {
        return BuildLaneColumns(tab, tab.Lane);
    }

    private static IReadOnlyList<BoardColumnGroup> BuildLaneColumns(TabState tab, string? laneKey)
    {
        var lane = Swimlanes.Filter(tab.Swimlane, laneKey, tab.Rows);

        return BoardColumns.Visible(tab.Board, lane, tab.ExpandedSubtasks, tab.Rows, tab.DoneCutoffDays);
    }

    /// <summary>
    /// Drops the marks (they would stay on cards no longer shown), puts the cursor back on the first column and a card of the columns now drawn (another lane was chosen), and
    /// makes the tab's selected row follow it.
    /// </summary>
    public static TabState Resync(TabState tab)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, null, null, ViewOf(tab));
        var first = columns.FirstOrDefault(column => column.Rows.Count > 0)?.Rows[0].Key;

        if (first is not null)
        {
            cursor = BoardMotion.Follow(columns, cursor, first, ViewOf(tab));
        }

        return Follow(tab with { Cursor = null, Marks = [], VisualAnchor = null }, columns, cursor);
    }

    /// <summary>The room the tab's board has, for the scroll maths (the same the renderer draws in).</summary>
    public static BoardViewport ViewOf(TabState tab)
    {
        return new BoardViewport(LaneStrip.BoardRows(tab), tab.ViewWidth);
    }

    /// <summary>
    /// The lane the body cell is on in the lane strip (#874), null when the strip is not shown or the cell is not on a
    /// chip.
    /// </summary>
    public static string? LaneAt(TabState tab, int width, int column, int row)
    {
        if (!LaneStrip.IsDrawn(tab) || row != 0)
        {
            return null;
        }

        return LaneStrip.Layout(LanesOf(tab), tab.Lane, width).KeyAt(column);
    }

    /// <summary>The lanes of the tab's grouping, kept per row list (empty without a grouping).</summary>
    public static IReadOnlyList<SwimlaneLane> LanesOf(TabState tab)
    {
        return tab.Columns.GetLanes(tab);
    }

    private static BoardHit? HitAt(
        TabState tab,
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        int width,
        int column,
        int row)
    {
        var region = LaneRegions.FocusRegion(tab);

        if (!region.Contains(row))
        {
            return null;
        }

        return BoardHits.At(columns, cursor, region.BoardRows, width, column, row - region.BoardTop, tab.Next is not null);
    }

    private static TabState Follow(TabState tab, IReadOnlyList<BoardColumnGroup> columns, BoardCursor cursor)
    {
        var moved = tab with { Cursor = cursor };

        if (BoardMotion.SelectedIssue(columns, cursor) is not { } issue)
        {
            return moved;
        }

        var index = tab.Columns.IndexOfKey(tab, issue.Key);

        // A row listed from an expanded parent is not a loaded row: only the key follows, so the cursor stays on it.
        if (index < 0)
        {
            return moved with { SelectedKey = issue.Key };
        }

        return moved with { Selected = index, SelectedKey = issue.Key };
    }

    private static int IndexOfKey(IReadOnlyList<IssueSummary> rows, string key)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Key == key)
            {
                return index;
            }
        }

        return -1;
    }
}
