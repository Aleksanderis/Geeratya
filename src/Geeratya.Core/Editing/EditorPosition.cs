namespace Geeratya.Core.Editing;

/// <summary>Where a text index sits in the visual rows: the row and the display columns before it on that row.</summary>
public readonly record struct EditorPosition(int Row, int Column);
