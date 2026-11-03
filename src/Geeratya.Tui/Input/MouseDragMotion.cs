namespace Geeratya.Tui.Input;

/// <summary>The held left button moved to another cell: where it went down, where the pointer is now.</summary>
/// <param name="FromColumn">The screen column of the press.</param>
/// <param name="FromRow">The screen row of the press.</param>
/// <param name="Column">The screen column of the pointer.</param>
/// <param name="Row">The screen row of the pointer.</param>
/// <param name="First">The first move of this drag.</param>
public readonly record struct MouseDragMotion(int FromColumn, int FromRow, int Column, int Row, bool First);
