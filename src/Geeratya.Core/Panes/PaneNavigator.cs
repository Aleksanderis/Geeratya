namespace Geeratya.Core.Panes;

/// <summary>Pure focus movement over a <see cref="PaneGrid"/>.</summary>
public static class PaneNavigator
{
    /// <summary>
    /// The pane reached from <paramref name="current"/> in <paramref name="direction"/>: among the panes that lie
    /// entirely beyond its edge and overlap it across the movement axis, the nearest one wins, then the one sharing
    /// the most cells (the first in grid order on a tie). <paramref name="current"/> itself when there is none, and
    /// also when it is not in the grid.
    /// </summary>
    public static string Move(PaneGrid grid, string current, PaneDirection direction)
    {
        if (grid.Find(current) is not { } from)
        {
            return current;
        }

        PaneSlot? best = null;
        var bestDistance = int.MaxValue;
        var bestOverlap = 0;

        foreach (var slot in grid.Slots)
        {
            if (slot.Id == from.Id)
            {
                continue;
            }

            var distance = Distance(from, slot, direction);
            var overlap = Overlap(from, slot, direction);

            if (distance < 0 || overlap <= 0)
            {
                continue;
            }

            if (distance < bestDistance || (distance == bestDistance && overlap > bestOverlap))
            {
                best = slot;
                bestDistance = distance;
                bestOverlap = overlap;
            }
        }

        return best?.Id ?? current;
    }

    // The gap between the facing edges; negative when the slot is not beyond the edge in that direction.
    private static int Distance(PaneSlot from, PaneSlot slot, PaneDirection direction)
    {
        return direction switch
        {
            PaneDirection.Left => from.Column - slot.ColumnEnd,
            PaneDirection.Right => slot.Column - from.ColumnEnd,
            PaneDirection.Up => from.Row - slot.RowEnd,
            _ => slot.Row - from.RowEnd,
        };
    }

    // How many grid cells the two panes share across the axis perpendicular to the movement.
    private static int Overlap(PaneSlot from, PaneSlot slot, PaneDirection direction)
    {
        var horizontal = direction is PaneDirection.Left or PaneDirection.Right;
        var start = horizontal ? Math.Max(from.Row, slot.Row) : Math.Max(from.Column, slot.Column);
        var end = horizontal ? Math.Min(from.RowEnd, slot.RowEnd) : Math.Min(from.ColumnEnd, slot.ColumnEnd);

        return end - start;
    }
}
