namespace Geeratya.Tui.Input;

/// <summary>A completed drag: the screen cell where the button went down and the one where it came up.</summary>
public readonly record struct MouseDrag(int FromColumn, int FromRow, int ToColumn, int ToRow);
