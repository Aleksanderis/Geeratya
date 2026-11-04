namespace Geeratya.Core.Boards;

/// <summary>The columns of a board that fit the terminal width, and how wide each one is.</summary>
/// <param name="First">The first column drawn.</param>
/// <param name="Count">How many columns are drawn.</param>
/// <param name="Total">How many columns the board has.</param>
/// <param name="Width">The terminal width the window was made for.</param>
public readonly record struct BoardWindow(int First, int Count, int Total, int Width)
{
    /// <summary>Columns hidden on the left.</summary>
    public int HiddenLeft => First;

    /// <summary>Columns hidden on the right.</summary>
    public int HiddenRight => Total - First - Count;

    /// <summary>
    /// The width of the <paramref name="position"/>th drawn column (0 for the first drawn). The widths add up to the
    /// terminal width: the first columns take the remainder, one cell each.
    /// </summary>
    public int ColumnWidth(int position)
    {
        if (Count <= 0)
        {
            return 0;
        }

        return (Width / Count) + (position < Width % Count ? 1 : 0);
    }

    /// <summary>
    /// The cells between the borders of the <paramref name="position"/>th drawn column: neighbours share their border,
    /// so every column but the last is one cell narrower than <see cref="ColumnWidth"/>, the last two.
    /// </summary>
    public int CellWidth(int position)
    {
        var width = ColumnWidth(position);

        return Math.Max(0, position == Count - 1 ? width - 2 : width - 1);
    }
}
