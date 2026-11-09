using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Views;

/// <summary>
/// Moves the selection of a tab in the columns layout. Pure on a <see cref="TabState"/>: the cursor says which card
/// is selected, and the tab's own selected row and key follow it so the list layout, a refresh and the issue detail
/// all see the same issue.
/// </summary>
public static class ColumnsNavigation
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
    /// A cell with neither is returned unchanged.
    /// </summary>
    public static TabState Place(TabState tab, int width, int column, int row)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        if (HitAt(tab, columns, cursor, width, column, row) is not { } hit)
        {
            return tab;
        }

        return Follow(tab, columns, BoardMotion.Place(columns, tab.Cursor, tab.SelectedKey, hit, ViewOf(tab)));
    }

    /// <summary>True when the body cell is on a card (not a column header or an empty cell).</summary>
    public static bool IsCardAt(TabState tab, int width, int column, int row)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, ViewOf(tab));

        var hit = HitAt(tab, columns, cursor, width, column, row);

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

        if (BoardCards.IsCompact(LaneStrip.BoardRows(tab), ListLayout.HeaderRows)
            || BoardHits.Locate(columns, cursor, LaneStrip.BoardRows(tab), width, column, row - LaneStrip.Offset(tab))
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
    /// columns (a flash jump). A tab without that card is returned with its cursor settled.
    /// </summary>
    public static TabState Select(TabState tab, string key)
    {
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
        var lane = Swimlanes.Filter(tab.Swimlane, tab.Lane, tab.Rows);

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
        if (!LaneStrip.IsShown(tab) || row != 0)
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
        var board = row - LaneStrip.Offset(tab);

        return BoardHits.At(columns, cursor, LaneStrip.BoardRows(tab), width, column, board, tab.Next is not null);
    }

    private static TabState Follow(TabState tab, IReadOnlyList<BoardColumnGroup> columns, BoardCursor cursor)
    {
        var moved = tab with { Cursor = cursor };

        if (BoardMotion.SelectedIssue(columns, cursor) is not { } issue)
        {
            return moved;
        }

        var index = IndexOfKey(tab.Rows, issue.Key);

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
