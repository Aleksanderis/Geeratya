using Geeratya.Core.Lists;
using Geeratya.Core.Text;

namespace Geeratya.Core.Boards;

/// <summary>
/// Finds the column and card under a cell of the columns layout, from the same measures the renderer draws with
/// (window, column widths, card pitch, scroll of each column). Pure.
/// </summary>
public static class BoardHits
{
    /// <summary>The header row of the columns inside the body: the top edge of the frame, where the titles sit.</summary>
    public const int HeaderRow = 0;

    /// <summary>
    /// The hit at a body cell, or null below the cards or outside the drawn columns.
    /// <paramref name="cursor"/> must be resolved for <paramref name="columns"/>.
    /// </summary>
    public static BoardHit? At(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        int visibleRows,
        int width,
        int column,
        int row,
        bool moreLoads = false)
    {
        if (columns.Count == 0 || row < HeaderRow || column < 0 || column >= width)
        {
            return null;
        }

        var window = BoardWindows.For(columns.Count, cursor.Column, width);

        if (window.Count == 1 && window.Total > 1 && row == HeaderRow)
        {
            return NeighbourAt(columns, cursor.Column, window.Width, column, moreLoads);
        }

        var chosen = ColumnAt(window, column);

        if (row == HeaderRow)
        {
            return new BoardHit(chosen, null);
        }

        return Locate(columns, cursor, visibleRows, width, column, row)?.Hit;
    }

    /// <summary>
    /// The card row under a body cell: the hit and which row of the card it is (summary, labels, key row ...), null
    /// below the cards, in a blank column area or on a one-line card (those are all key rows).
    /// </summary>
    public static BoardCardHit? Locate(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        int visibleRows,
        int width,
        int column,
        int row)
    {
        if (columns.Count == 0 || row <= HeaderRow || column < 0 || column >= width)
        {
            return null;
        }

        var window = BoardWindows.For(columns.Count, cursor.Column, width);
        var chosen = ColumnAt(window, column);
        var offset = row - BoardCards.TopRows;

        if (BoardCards.IsCompact(visibleRows, ListLayout.HeaderRows))
        {
            var card = cursor.Scrolls[chosen] + offset;
            var shown = BoardCards.Visible(visibleRows, ListLayout.HeaderRows);

            if (offset >= shown || card >= columns[chosen].Rows.Count)
            {
                return null;
            }

            return new BoardCardHit(new BoardHit(chosen, card), new CardRow(CardRowKind.Key, 0));
        }

        var rows = BoardCards.CardRows(visibleRows, ListLayout.HeaderRows);
        var placed = BoardColumnLayout.Place(
            columns[chosen],
            cursor.Scrolls[chosen],
            window.CellWidth(chosen - window.First),
            rows);

        if (BoardColumnLayout.At(placed, offset) is not { } found)
        {
            return null;
        }

        return new BoardCardHit(new BoardHit(chosen, found.Card), found.Rows[offset - found.Top]);
    }

    // The header of a one-column window names the neighbours ("‹ Name n  Title  Name n ›"): a click on a name steps to
    // that column. The cells follow the text the renderer draws (BoardHeaderText), after the "┌─" corner.
    private static BoardHit? NeighbourAt(
        IReadOnlyList<BoardColumnGroup> columns,
        int selected,
        int windowWidth,
        int cell,
        bool moreLoads)
    {
        var edge = windowWidth - 1;
        var start = BoardHeaderText.CornerWidth;

        if (selected > 0)
        {
            var end = start + DisplayWidth.Of(BoardHeaderText.LeftNeighbour(columns[selected - 1], moreLoads));

            if (cell >= start && cell < Math.Min(end, edge))
            {
                return new BoardHit(selected - 1, null);
            }

            start = end;
        }

        start += DisplayWidth.Of(BoardHeaderText.Title(columns[selected], moreLoads));

        if (selected < columns.Count - 1)
        {
            var end = start + DisplayWidth.Of(BoardHeaderText.RightNeighbour(columns[selected + 1], moreLoads));

            if (cell >= start && cell < Math.Min(end, edge))
            {
                return new BoardHit(selected + 1, null);
            }
        }

        return null;
    }

    /// <summary>The column under a body cell, whatever the row; null outside the drawn columns.</summary>
    public static int? ColumnAtCell(IReadOnlyList<BoardColumnGroup> columns, BoardCursor cursor, int width, int column)
    {
        if (columns.Count == 0 || column < 0 || column >= width)
        {
            return null;
        }

        return ColumnAt(BoardWindows.For(columns.Count, cursor.Column, width), column);
    }

    private static int ColumnAt(BoardWindow window, int cell)
    {
        var start = 0;

        for (var position = 0; position < window.Count; position++)
        {
            start += window.ColumnWidth(position);

            if (cell < start)
            {
                return window.First + position;
            }
        }

        return window.First + window.Count - 1;
    }
}
