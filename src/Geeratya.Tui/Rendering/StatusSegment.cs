namespace Geeratya.Tui.Rendering;

/// <summary>A run of the status bar drawn in a palette role of its own: <paramref name="Width"/> cells from <paramref name="Start"/>.</summary>
public sealed record StatusSegment(int Start, int Width, string Text, PaletteRole Role);
