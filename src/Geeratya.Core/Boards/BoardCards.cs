namespace Geeratya.Core.Boards;

/// <summary>
/// Fixed measures of a card in the columns layout that the reducer and the renderer must agree on
/// (<c>docs/ux-screens.md</c>, "Columns layout").
/// </summary>
public static class BoardCards
{
    /// <summary>Rows of a card in full form: top border, key line, two summary lines and bottom border.</summary>
    public const int FullRows = 5;

    /// <summary>Rows under the cards that close the column frames.</summary>
    public const int FrameRows = 1;

    /// <summary>The body height (header rows included) below which cards collapse to one line.</summary>
    public const int CompactBelowBodyRows = 12;

    /// <summary>Rows between the first row of one card and the first row of the next.</summary>
    public static int Pitch(int visibleRows, int headerRows)
    {
        return IsCompact(visibleRows, headerRows) ? 1 : FullRows;
    }

    /// <summary>Whether cards are drawn on one line for a card area of <paramref name="visibleRows"/> rows.</summary>
    public static bool IsCompact(int visibleRows, int headerRows)
    {
        return visibleRows + headerRows < CompactBelowBodyRows;
    }

    /// <summary>
    /// How many cards a column shows at once. Zero while there is no room.
    /// </summary>
    public static int Visible(int visibleRows, int headerRows)
    {
        var rows = visibleRows - FrameRows;

        if (rows <= 0)
        {
            return 0;
        }

        return IsCompact(visibleRows, headerRows) ? rows : Math.Max(1, rows / FullRows);
    }
}
