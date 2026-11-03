using Geeratya.Core.Lists;

namespace Geeratya.Core.Boards;

/// <summary>
/// Fixed measures of a card in the columns layout that the reducer and the renderer must agree on
/// (<c>docs/ux-screens.md</c>, "Columns layout").
/// </summary>
public static class BoardCards
{
    /// <summary>Rows of a card in full form: top border, key line, two summary lines and bottom border.</summary>
    public const int FullRows = 5;

    /// <summary>Rows above the cards: the top edge of the frame, which carries the column titles.</summary>
    public const int TopRows = 1;

    /// <summary>Rows under the cards that close the column frames.</summary>
    public const int FrameRows = 1;

    /// <summary>The body height below which cards collapse to one line.</summary>
    public const int CompactBelowBodyRows = 12;

    /// <summary>Rows between the first row of one card and the first row of the next.</summary>
    public static int Pitch(int visibleRows, int headerRows)
    {
        return IsCompact(visibleRows, headerRows) ? 1 : FullRows;
    }

    /// <summary>Whether cards are drawn on one line for a card area of <paramref name="visibleRows"/> rows.</summary>
    public static bool IsCompact(int visibleRows, int headerRows)
    {
        return BodyRows(visibleRows, headerRows) < CompactBelowBodyRows;
    }

    /// <summary>The body height for a list's <paramref name="visibleRows"/>: its header rows, items and bottom edge.</summary>
    public static int BodyRows(int visibleRows, int headerRows)
    {
        return visibleRows + headerRows + ListLayout.FooterRows;
    }

    /// <summary>The rows between the top and the bottom edge of the frame.</summary>
    public static int CardRows(int visibleRows, int headerRows)
    {
        return Math.Max(0, BodyRows(visibleRows, headerRows) - TopRows - FrameRows);
    }

    /// <summary>
    /// How many cards a column shows at once. Zero while there is no room.
    /// </summary>
    public static int Visible(int visibleRows, int headerRows)
    {
        var rows = CardRows(visibleRows, headerRows);

        if (rows <= 0)
        {
            return 0;
        }

        return IsCompact(visibleRows, headerRows) ? rows : Math.Max(1, rows / FullRows);
    }
}
