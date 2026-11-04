namespace Geeratya.Core.Panes;

/// <summary>
/// The inner scroll of one pane: how many rows it holds, how many fit and which is the first shown. All members are
/// pure; a pane never scrolls past its last row, and a pane that fits scrolls not at all.
/// </summary>
/// <param name="Offset">The first visible row.</param>
/// <param name="Total">The rows the content has.</param>
/// <param name="Viewport">The rows that fit in the frame.</param>
public sealed record PaneScroll(int Offset, int Total, int Viewport)
{
    /// <summary>The largest useful offset.</summary>
    public int MaxOffset => Math.Max(0, Total - Math.Max(0, Viewport));

    /// <summary>The offset kept inside 0 to <see cref="MaxOffset"/>.</summary>
    public int ClampedOffset => Math.Clamp(Offset, 0, MaxOffset);

    /// <summary>True when the content is longer than the frame, so a scrollbar is drawn.</summary>
    public bool Scrollable => Viewport > 0 && Total > Viewport;

    /// <summary>The scroll moved by <paramref name="rows"/> (negative is up), clamped.</summary>
    public PaneScroll By(int rows)
    {
        var moved = (long)ClampedOffset + rows;

        return this with { Offset = (int)Math.Clamp(moved, 0, MaxOffset) };
    }

    /// <summary>The scroll with the content and frame size replaced; the offset is re-clamped.</summary>
    public PaneScroll Resized(int total, int viewport)
    {
        var resized = this with { Total = Math.Max(0, total), Viewport = Math.Max(0, viewport) };

        return resized with { Offset = resized.ClampedOffset };
    }

    /// <summary>The scroll moved the least that shows <paramref name="row"/>; a row outside the content is clamped.</summary>
    public PaneScroll Reveal(int row)
    {
        var target = Math.Clamp(row, 0, Math.Max(0, Total - 1));
        var offset = ClampedOffset;

        if (target < offset)
        {
            offset = target;
        }
        else if (Viewport > 0 && target >= offset + Viewport)
        {
            offset = target - Viewport + 1;
        }

        return this with { Offset = Math.Clamp(offset, 0, MaxOffset) };
    }

    /// <summary>The position chip text, rows one-based: <c>1-4 of 14</c>; empty content gives <c>0 of 0</c>.</summary>
    public string Chip()
    {
        if (Total == 0)
        {
            return "0 of 0";
        }

        var first = ClampedOffset + 1;
        var last = Math.Min(Total, ClampedOffset + Math.Max(1, Viewport));

        return $"{first}-{last} of {Total}";
    }
}
