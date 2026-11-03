namespace Geeratya.Core.Mouse;

/// <summary>
/// Turns a screen cell into an area. Pure: the same context and cell always give the same answer. The areas are rows of
/// the frame (tab bar, banner, body, status line); finer targets (a tab, a badge) are resolved by their own parts.
/// While an overlay is open the rectangle its renderer reports (<see cref="MouseTarget.OverlayBox"/>, or
/// <see cref="MouseTarget.ConfirmBox"/>) is its box and everything else in the body is the backdrop; an overlay without
/// a reported box keeps only the rows outside the body as backdrop, so a click in the body does nothing.
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
            var target = context.OverlayOpen ? MouseTarget.Backdrop : HeaderTarget(context, row, column);

            return new MouseHit(target, row, column);
        }

        if (row == statusRow)
        {
            if (context.ConfirmOpen)
            {
                return new MouseHit(MouseTarget.Backdrop, 0, column);
            }

            if (context.OverlayOpen)
            {
                return null;
            }

            if (context.LaneChip is { } lane && column >= lane.Start && column < lane.End)
            {
                return new MouseHit(MouseTarget.LaneChip, 0, column);
            }

            var message = context.StatusMessage is { } cells && column >= cells.Start && column < cells.End;

            return new MouseHit(message ? MouseTarget.StatusMessage : MouseTarget.Status, 0, column);
        }

        if (context.OverlayOpen)
        {
            return OverlayHit(context, column, row);
        }

        var bodyTarget = context.DetailOpen ? MouseTarget.Detail : MouseTarget.Lists;

        return new MouseHit(bodyTarget, row - header, column);
    }

    // The topmost region under the cell; the rest of the overlay, and the body behind it, does nothing.
    private static MouseHit? OverlayHit(MouseContext context, int column, int row)
    {
        var regions = context.Regions;

        for (var index = regions.Count - 1; index >= 0; index--)
        {
            if (regions[index].Contains(column, row))
            {
                return new MouseHit(regions[index].Target, regions[index].Index, column);
            }
        }

        // Outside the box of the overlay a click is the backdrop: the confirm dialog answers no, any other overlay
        // closes as Esc does. An overlay that reports no box keeps the old rule: the body does nothing.
        var hasBox = context.ConfirmOpen || regions.Any(static region => region.Target == MouseTarget.OverlayBox);

        return hasBox ? new MouseHit(MouseTarget.Backdrop, row, column) : null;
    }

    private static MouseTarget HeaderTarget(MouseContext context, int row, int column)
    {
        if (row != 0)
        {
            return MouseTarget.Banner;
        }

        if (context.CommandsChip is { } commands && column >= commands.Start && column < commands.End)
        {
            return MouseTarget.CommandsChip;
        }

        if (context.JumpChip is { } chip && column >= chip.Start && column < chip.End)
        {
            return MouseTarget.JumpChip;
        }

        if (context.TabClose is { } close && column >= close.Start && column < close.End)
        {
            return MouseTarget.TabClose;
        }

        var button = context.NewTabButton;

        if (button is { } cells && column >= cells.Start && column < cells.End)
        {
            return MouseTarget.NewTabButton;
        }

        return MouseTarget.Tabs;
    }
}
