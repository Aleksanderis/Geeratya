using Geeratya.Core.Lists;

namespace Geeratya.Core.Boards;

/// <summary>
/// Fixed measures of a card in the columns layout that the reducer and the renderer must agree on
/// (<c>docs/ux-screens.md</c>, "Columns layout").
/// </summary>
public static class BoardCards
{
    /// <summary>Cells of margin on each side of a card in a column; the labeler and the renderer share it.</summary>
    public const int CardMargin = 1;

    /// <summary>Cells a child card is indented inside the shared frame of its parent.</summary>
    public const int ChildIndent = 2;

    /// <summary>The narrowest box a card is drawn in; narrower columns show blank cells.</summary>
    public const int MinimumBoxWidth = 8;

    /// <summary>Rows above the cards: the top edge of the frame, which carries the column titles.</summary>
    public const int TopRows = 1;

    /// <summary>Rows under the cards that close the column frames.</summary>
    public const int FrameRows = 1;

    /// <summary>The body height below which cards collapse to one line.</summary>
    public const int CompactBelowBodyRows = 12;

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
    /// How many cards a column is sure to show at once: cards differ in height (<see cref="BoardCardPlan"/>), so a
    /// full card is counted at its tallest. The columns usually draw more (<see cref="BoardColumnLayout"/> places
    /// them by their real height), never fewer, so a cursor inside this window is always drawn. Zero while there is no
    /// room.
    /// </summary>
    public static int Visible(int visibleRows, int headerRows)
    {
        var rows = CardRows(visibleRows, headerRows);

        if (rows <= 0)
        {
            return 0;
        }

        return IsCompact(visibleRows, headerRows) ? rows : Math.Max(1, rows / BoardCardPlan.MaxRows);
    }
}
