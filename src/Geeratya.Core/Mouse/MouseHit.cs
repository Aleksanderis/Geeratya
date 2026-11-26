namespace Geeratya.Core.Mouse;

/// <summary>Where a pointer event landed: the area, and the position inside it (zero based).</summary>
/// <param name="Target">The area.</param>
/// <param name="Row">The row inside the area (the body row for lists and details).</param>
/// <param name="Column">The cell column on the screen.</param>
/// <param name="Region">The overlay rectangle that was hit, null outside an overlay.</param>
// Row and Column are read by part B (row selection, tab and chip hit areas).
public sealed record MouseHit(MouseTarget Target, int Row, int Column, OverlayRegion? Region = null);
