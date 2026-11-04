namespace Geeratya.Core.Detail;

/// <summary>
/// Where the wide overlay of the issue detail and the create form sits in the body (<c>docs/ux-screens.md</c>, "Where
/// it is drawn"): the terminal width minus four columns and the body height minus two rows, centred, at most 160
/// columns wide; below 100 columns it fills the body, with no margin and no backdrop. Pure.
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

    /// <summary>The overlay for a terminal <paramref name="width"/> and a body of <paramref name="bodyHeight"/> rows.</summary>
    public static DetailOverlayGeometry Place(int width, int bodyHeight)
    {
        if (width < FillBelow || bodyHeight <= MarginRows)
        {
            return new DetailOverlayGeometry(0, 0, Math.Max(0, width), Math.Max(0, bodyHeight), true);
        }

        var boxWidth = Math.Min(width - MarginColumns, MaxWidth);
        var boxHeight = bodyHeight - MarginRows;

        return new DetailOverlayGeometry((width - boxWidth) / 2, MarginRows / 2, boxWidth, boxHeight, false);
    }

    /// <summary>Whether the body cell (<paramref name="column"/>, <paramref name="row"/>) is inside the overlay.</summary>
    public bool Contains(int column, int row)
    {
        return column >= Left && column < Left + Width && row >= Top && row < Top + Height;
    }
}
