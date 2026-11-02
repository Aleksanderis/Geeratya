namespace Geeratya.Core.Mouse;

/// <summary>Where a pointer event landed: the area, and the position inside it (zero based).</summary>
/// <param name="Target">The area.</param>
/// <param name="Row">The row inside the area (the body row for lists and details).</param>
/// <param name="Column">The cell column on the screen.</param>
public sealed record MouseHit(MouseTarget Target, int Row, int Column);
