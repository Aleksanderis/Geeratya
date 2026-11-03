using Geeratya.Core.Lists;

namespace Geeratya.Core.Mouse;

/// <summary>
/// How fast the cursor follows a pointer that rests outside the list body while the left button is held: one row just
/// outside, one more for every two rows of distance, at most six rows per step. The shell steps every
/// <see cref="Interval"/>; this type only holds the speed table. Pure.
/// </summary>
public static class DragAutoScroll
{
    /// <summary>The time between two steps.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(80);

    /// <summary>The most rows one step moves.</summary>
    public const int MaxRows = 6;

    /// <summary>The rows one step moves for a pointer that many rows outside the body (zero or less: none).</summary>
    public static int RowsFor(int distance)
    {
        return distance <= 0 ? 0 : Math.Min(MaxRows, 1 + (distance - 1) / 2);
    }

    /// <summary>
    /// The signed rows per step for a pointer on the screen row: negative above the first list row, positive below the
    /// last body row, zero inside the body.
    /// </summary>
    public static int StepFor(MouseContext context, int screenRow)
    {
        var firstRow = context.HeaderRows + ListLayout.HeaderRows;
        var lastRow = context.Height - 2;

        if (screenRow < firstRow)
        {
            return -RowsFor(firstRow - screenRow);
        }

        return screenRow > lastRow ? RowsFor(screenRow - lastRow) : 0;
    }
}
