namespace Geeratya.Core.Mouse;

/// <summary>
/// A clickable rectangle of an open overlay, in screen cells, as the renderer placed it (so the hit test never
/// guesses the layout). <paramref name="Index"/> is what the target names: a choice, a field, a popup row.
/// </summary>
/// <param name="Target">What a click here means.</param>
/// <param name="Top">The first row.</param>
/// <param name="Left">The first screen column.</param>
/// <param name="Width">Columns covered.</param>
/// <param name="Height">Rows covered.</param>
/// <param name="Index">The choice, field or popup row the rectangle stands for.</param>
public sealed record OverlayRegion(MouseTarget Target, int Top, int Left, int Width, int Height, int Index)
{
    public bool Contains(int column, int row)
    {
        return row >= Top && row < Top + Height && column >= Left && column < Left + Width;
    }

    /// <summary>The same rectangle <paramref name="rows"/> lower (body rows to screen rows).</summary>
    public OverlayRegion Shifted(int rows)
    {
        return this with { Top = Top + rows };
    }
}
