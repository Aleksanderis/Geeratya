namespace Geeratya.Core.Detail;

/// <summary>
/// Where the wide overlay of the issue detail and the create form sits in the body (<c>docs/ux-screens.md</c>, "Where
/// it is drawn"): the terminal width minus four columns, centred, at most 160 columns wide; the body height minus a
/// top margin of four rows (so the column titles and first card rows of the view show above it) and one row below;
/// below 100 columns it fills the body, with no margin and no backdrop. Pure.
/// </summary>
/// <param name="Left">The first column of the overlay.</param>
/// <param name="Top">The first body row of the overlay.</param>
/// <param name="Width">The width in columns.</param>
/// <param name="Height">The height in rows.</param>
/// <param name="Fills">The overlay fills the whole body, so nothing of the view shows behind it.</param>
public sealed record DetailOverlayGeometry(int Left, int Top, int Width, int Height, bool Fills)
{
    /// <summary>The widest overlay.</summary>
    public const int MaxWidth = 160;

    /// <summary>Terminal widths below this fill the body.</summary>
    public const int FillBelow = 100;

    private const int MarginColumns = 4;
    private const int MarginRows = 2;
    private const int MinTopRows = 1;
    private const int TopRows = 4;
    private const int MinHeight = 8;

    /// <summary>The overlay for <paramref name="width"/> terminal columns and <paramref name="bodyHeight"/> body rows.</summary>
    public static DetailOverlayGeometry Place(int width, int bodyHeight)
    {
        if (width < FillBelow || bodyHeight <= MarginRows)
        {
            return new DetailOverlayGeometry(0, 0, Math.Max(0, width), Math.Max(0, bodyHeight), true);
        }

        var boxWidth = Math.Min(width - MarginColumns, MaxWidth);
        var top = Math.Min(TopRows, Math.Max(MinTopRows, bodyHeight - MinHeight - 1));
        var boxHeight = bodyHeight - top - 1;

        return new DetailOverlayGeometry((width - boxWidth) / 2, top, boxWidth, boxHeight, false);
    }

    /// <summary>Whether body cell (<paramref name="column"/>, <paramref name="row"/>) is inside the overlay.</summary>
    public bool Contains(int column, int row)
    {
        return column >= Left && column < Left + Width && row >= Top && row < Top + Height;
    }
}
