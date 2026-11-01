namespace Geeratya.AnsiToSvg;

/// <summary>One grapheme at a column; Width is 2 for a wide character, whose next column is left empty.</summary>
public readonly record struct Cell(string Text, int Width, CellStyle Style);
