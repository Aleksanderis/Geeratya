namespace Geeratya.Core.Mouse;

/// <summary>
/// Turns a screen cell into an area. Pure: the same context and cell always give the same answer. The areas are rows of
/// the frame (tab bar, banner, body, status line); finer targets (a tab, a badge) are resolved by their own parts.
/// While an overlay is open only the rows outside the body count as the backdrop: the overlay's own rectangle is not
/// known here, so a click in the body then does nothing rather than closing what the user clicked on.
/// </summary>
public static class MouseHitTester
{
    /// <summary>The area at the cell, or null when it is outside the screen or has no meaning right now.</summary>
    public static MouseHit? Locate(MouseContext context, int column, int row)
    {
        if (column < 0 || row < 0 || column >= context.Width || row >= context.Height)
        {
            return null;
        }

        var statusRow = context.Height - 1;
        var header = Math.Min(context.HeaderRows, Math.Max(0, statusRow));

        if (row < header)
        {
            var target = context.OverlayOpen ? MouseTarget.Backdrop : HeaderTarget(row);

            return new MouseHit(target, row, column);
        }

        if (row == statusRow)
        {
            return context.OverlayOpen ? null : new MouseHit(MouseTarget.Status, 0, column);
        }

        if (context.OverlayOpen)
        {
            return null;
        }

        var bodyTarget = context.DetailOpen ? MouseTarget.Detail : MouseTarget.Lists;

        return new MouseHit(bodyTarget, row - header, column);
    }

    private static MouseTarget HeaderTarget(int row)
    {
        return row == 0 ? MouseTarget.Tabs : MouseTarget.Banner;
    }
}
