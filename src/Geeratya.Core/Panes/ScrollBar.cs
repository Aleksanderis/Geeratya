namespace Geeratya.Core.Panes;

/// <summary>Pure layout of a pane's scrollbar column.</summary>
public static class ScrollBar
{
    /// <summary>
    /// The cells of a bar <paramref name="height"/> rows high for <paramref name="scroll"/>: an up arrow, a down arrow
    /// and between them the track with a thumb sized by the share of content in view (at least one cell) and placed
    /// by the offset (at the top at offset 0, at the bottom at the last offset). Empty when the content fits or
    /// there is no room for the arrows and one cell between them.
    /// </summary>
    public static IReadOnlyList<ScrollCell> Cells(PaneScroll scroll, int height)
    {
        if (!scroll.Scrollable || height < 3)
        {
            return [];
        }

        var inner = height - 2;
        var thumb = Math.Clamp((int)((long)inner * scroll.Viewport / scroll.Total), 1, inner);
        var travel = inner - thumb;
        var start = (int)Math.Round((double)travel * scroll.ClampedOffset / scroll.MaxOffset);
        var cells = new ScrollCell[height];

        cells[0] = ScrollCell.Up;
        cells[height - 1] = ScrollCell.Down;

        for (var index = 0; index < inner; index++)
        {
            cells[index + 1] = index >= start && index < start + thumb ? ScrollCell.Thumb : ScrollCell.Track;
        }

        return cells;
    }
}
