namespace Geeratya.Core.Detail;

/// <summary>
/// Where the wide overlay of the issue detail and the create form sits in the body (<c>docs/ux-screens.md</c>, "Where
/// it is drawn"): a card, the terminal width minus four columns, centred, at most 160 columns wide, the body height
/// minus a top margin of four rows (so the column titles and first card rows of the view show above it) and one row
/// below; the panes sit inside the card's frame and padding. Below 100 columns it fills the body, with no card, no
/// margin and no backdrop. Pure.
/// </summary>
/// <param name="Left">The first column of the panes (inside the card).</param>
/// <param name="Top">The first body row of the panes.</param>
/// <param name="Width">The width of the panes in columns.</param>
/// <param name="Height">The height of the panes in rows.</param>
/// <param name="Fills">The overlay fills the whole body, so nothing of the view shows behind it.</param>
public sealed record DetailOverlayGeometry(int Left, int Top, int Width, int Height, bool Fills)
{
    /// <summary>The widest overlay card.</summary>
    public const int MaxWidth = 160;

    /// <summary>Terminal widths below this fill the body.</summary>
    public const int FillBelow = 100;

    /// <summary>Columns between the card's edge and the panes: the frame and one column of padding.</summary>
    public const int CardInsetColumns = 2;

    /// <summary>Rows between the card's edge and the panes: the frame.</summary>
    public const int CardInsetRows = 1;

    private const int MarginColumns = 4;
    private const int MarginRows = 2;
    private const int MinTopRows = 1;
    private const int TopRows = 4;
    private const int MinHeight = 10;

    /// <summary>The first column of the card (its frame); the panes start <see cref="CardInsetColumns"/> later.</summary>
    public int CardLeft => Fills ? Left : Left - CardInsetColumns;

    /// <summary>The first body row of the card (its frame).</summary>
    public int CardTop => Fills ? Top : Top - CardInsetRows;

    /// <summary>The width of the card, frame and padding included.</summary>
    public int CardWidth => Fills ? Width : Width + (2 * CardInsetColumns);

    /// <summary>The height of the card, frame included.</summary>
    public int CardHeight => Fills ? Height : Height + (2 * CardInsetRows);

    /// <summary>The overlay for <paramref name="width"/> terminal columns and <paramref name="bodyHeight"/> body rows.</summary>
    public static DetailOverlayGeometry Place(int width, int bodyHeight)
    {
        if (width < FillBelow || bodyHeight <= MarginRows)
        {
            return new DetailOverlayGeometry(0, 0, Math.Max(0, width), Math.Max(0, bodyHeight), true);
        }

        var cardWidth = Math.Min(width - MarginColumns, MaxWidth);
        var top = Math.Min(TopRows, Math.Max(MinTopRows, bodyHeight - MinHeight - 1));
        var cardHeight = bodyHeight - top - 1;
        var left = (width - cardWidth) / 2;

        return new DetailOverlayGeometry(
            left + CardInsetColumns,
            top + CardInsetRows,
            cardWidth - (2 * CardInsetColumns),
            Math.Max(0, cardHeight - (2 * CardInsetRows)),
            false);
    }

    /// <summary>Whether body cell (<paramref name="column"/>, <paramref name="row"/>) is inside the card.</summary>
    public bool Contains(int column, int row)
    {
        return column >= CardLeft && column < CardLeft + CardWidth && row >= CardTop && row < CardTop + CardHeight;
    }
}
