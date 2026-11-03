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
        var cursor = BoardMotion.MoveCard(columns, tab.Cursor, tab.SelectedKey, move, CardsVisible(tab));

        return Follow(tab, columns, cursor);
    }

    /// <summary>
    /// A click: places the cursor on the card or column header at a body cell drawn for <paramref name="width"/>.
    /// A cell with neither is returned unchanged.
    /// </summary>
    public static TabState Place(TabState tab, int width, int column, int row)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, CardsVisible(tab));

        if (BoardHits.At(columns, cursor, tab.VisibleRows, width, column, row, tab.Next is not null) is not { } hit)
        {
            return tab;
        }

        return Follow(tab, columns, BoardMotion.Place(columns, tab.Cursor, tab.SelectedKey, hit, CardsVisible(tab)));
    }

    /// <summary>True when the body cell is on a card (not a column header or an empty cell).</summary>
    public static bool IsCardAt(TabState tab, int width, int column, int row)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, CardsVisible(tab));

        var hit = BoardHits.At(columns, cursor, tab.VisibleRows, width, column, row, tab.Next is not null);

        return hit is { Card: not null };
    }

    /// <summary>
    /// A drag: how many columns the pointer moved from the card at the first cell to the column under the second
    /// (positive to the right), or null when the drag did not start on a card, ended outside the columns, or the
    /// layout shows one column at a time (nothing to drop on).
    /// </summary>
    public static int? DragSteps(TabState tab, int width, int fromColumn, int fromRow, int toColumn)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, CardsVisible(tab));

        if (BoardWindows.For(columns.Count, cursor.Column, width).Count < 2
            || BoardHits.At(columns, cursor, tab.VisibleRows, width, fromColumn, fromRow, tab.Next is not null)
                is not { Card: not null } source
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
        var cursor = BoardMotion.MoveColumn(columns, tab.Cursor, tab.SelectedKey, move, CardsVisible(tab));

        return Follow(tab, columns, cursor);
    }

    /// <summary>
    /// Moves the cursor to the card with <paramref name="key"/> after its status changed, keeping the scroll offsets
    /// of the columns. A tab without a cursor, or without that card, is returned as it is.
    /// </summary>
    public static TabState FollowCard(TabState tab, string key)
    {
        if (tab.Cursor is not { } cursor)
        {
            return tab;
        }

        var columns = ColumnsOf(tab);

        return Follow(tab, columns, BoardMotion.Follow(columns, cursor, key, CardsVisible(tab)));
    }

    /// <summary>The issue under the cursor, null when the selected column is empty or the tab has no rows.</summary>
    public static IssueSummary? SelectedIssue(TabState tab)
    {
        var columns = ColumnsOf(tab);

        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, CardsVisible(tab));

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

        var cursor = BoardMotion.Resolve(columns, tab.Cursor, tab.SelectedKey, CardsVisible(tab));

        return new ColumnSelection(columns[cursor.Column].Rows, cursor.Card, columns[cursor.Column].Nested);
    }

    /// <summary>The columns the layout draws for the tab's rows.</summary>
    public static IReadOnlyList<BoardColumnGroup> ColumnsOf(TabState tab)
    {
        var lane = Swimlanes.Filter(tab.Swimlane, tab.Lane, tab.Rows);

        return BoardColumns.Visible(tab.Board, lane, tab.ExpandedSubtasks, tab.Rows);
    }

    /// <summary>
    /// Drops the marks (they would stay on cards no longer shown), puts the cursor back on the first column and a card of the columns now drawn (another lane was chosen), and
    /// makes the tab's selected row follow it.
    /// </summary>
    public static TabState Resync(TabState tab)
    {
        var columns = ColumnsOf(tab);
        var cursor = BoardMotion.Resolve(columns, null, null, CardsVisible(tab));
        var first = columns.FirstOrDefault(column => column.Rows.Count > 0)?.Rows[0].Key;

        if (first is not null)
        {
            cursor = BoardMotion.Follow(columns, cursor, first, CardsVisible(tab));
        }

        return Follow(tab with { Cursor = null, Marks = [], VisualAnchor = null }, columns, cursor);
    }

    private static int CardsVisible(TabState tab)
    {
        return BoardCards.Visible(tab.VisibleRows, ListLayout.HeaderRows);
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
