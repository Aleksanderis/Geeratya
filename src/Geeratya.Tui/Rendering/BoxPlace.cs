namespace Geeratya.Tui.Rendering;

/// <summary>Where a box sits in the body: its top row, left column and width.</summary>
/// <param name="Top">The body row of the box's top edge.</param>
/// <param name="Left">The body column of the box's left edge.</param>
/// <param name="Width">The box width in columns.</param>
public readonly record struct BoxPlace(int Top, int Left, int Width);
