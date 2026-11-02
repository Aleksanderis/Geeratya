using Geeratya.Core.Lists;

namespace Geeratya.Core.Boards;

/// <summary>
/// Finds the column and card under a cell of the columns layout, from the same measures the renderer draws with
/// (window, column widths, card pitch, scroll of each column). Pure.
/// </summary>
public static class BoardHits
{
    /// <summary>The header row of the columns inside the body (the title is row 0).</summary>
    public const int HeaderRow = 1;

    /// <summary>
    /// The hit at a body cell, or null on the title, below the cards or outside the drawn columns.
    /// <paramref name="cursor"/> must be resolved for <paramref name="columns"/>.
    /// </summary>
    public static BoardHit? At(
        IReadOnlyList<BoardColumnGroup> columns,
        BoardCursor cursor,
        int visibleRows,
        int width,
        int column,
        int row)
    {
        if (columns.Count == 0 || row < HeaderRow || column < 0 || column >= width)
        {
            return null;
        }

        var window = BoardWindows.For(columns.Count, cursor.Column, width);

        if (window.Count == 1 && window.Total > 1 && row == HeaderRow)
        {
            return null;
        }

        var chosen = ColumnAt(window, column);

        if (row == HeaderRow)
        {
            return new BoardHit(chosen, null);
        }

        var pitch = BoardCards.Pitch(visibleRows, ListLayout.HeaderRows);
        var offset = row - ListLayout.HeaderRows;
        var shown = BoardCards.Visible(visibleRows, ListLayout.HeaderRows);
        var card = cursor.Scrolls[chosen] + (offset / pitch);

        if (offset >= shown * pitch || card >= columns[chosen].Rows.Count)
        {
            return null;
        }

        return new BoardHit(chosen, card);
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
