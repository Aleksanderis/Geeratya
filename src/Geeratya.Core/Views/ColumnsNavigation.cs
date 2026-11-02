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
        return BoardColumns.Visible(tab.Board, tab.Rows);
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

        return index < 0 ? moved : moved with { Selected = index, SelectedKey = issue.Key };
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
