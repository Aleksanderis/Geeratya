namespace Geeratya.Tui.Rendering;

/// <summary>Where the rows of an edited text sit on the screen: the first row and column, the columns and the rows.</summary>
internal readonly record struct TextBox(int Top, int Left, int Width, int Rows);
