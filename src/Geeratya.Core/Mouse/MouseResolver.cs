using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Mouse;

/// <summary>Resolves a pointer event to the commands the state layer runs: hit test, then the mouse table.</summary>
public static class MouseResolver
{
    /// <summary>The parts of the mouse table that are wired today; rows of later parts have no effect yet.</summary>
    public const string WiredParts = "ABOMV";

    private static readonly IReadOnlyList<Command> _none = [];

    /// <summary>
    /// The commands for the gesture at the cell, empty when nothing is bound there. A click, double-click or
    /// right-click on a list or an opened issue first places the cursor under the pointer, then runs the action.
    /// </summary>
    public static IReadOnlyList<Command> Resolve(
        MouseContext context,
        int column,
        int row,
        MouseGesture gesture,
        DateTimeOffset at)
    {
        if (gesture is MouseGesture.WheelUp or MouseGesture.WheelDown && context.WheelTarget is { } wheeled)
        {
            return Wheeled(context, wheeled, column, row, gesture, at);
        }

        if (MouseHitTester.Locate(context, column, row) is not { } hit)
        {
            return _none;
        }

        if (IsOverlayRow(hit.Target))
        {
            return OverlayClick(hit, gesture, at);
        }

        if (hit.Target == MouseTarget.Tabs)
        {
            return TabClick(context, hit, gesture, at);
        }

        if (hit.Target == MouseTarget.TabClose && gesture == MouseGesture.Click)
        {
            return AreaAt(context, hit.Column) is { } closed ? [new TabClosed(closed.Tab)] : _none;
        }

        var inBody = hit.Target is MouseTarget.Lists or MouseTarget.Detail;

        if (inBody && gesture == MouseGesture.Click)
        {
            return [new PointerPlaced(column, hit.Row, context.Width)];
        }

        if (hit.Target == MouseTarget.Banner && !context.BannerShown)
        {
            return _none;
        }

        if (DefaultMouseMap.Find(hit.Target, gesture) is not { } found || !WiredParts.Contains(found.Part))
        {
            return _none;
        }

        var action = new ActionRequested(found.Action, found.Count, at);

        // The row under the pointer is the target of the action, not the row the cursor happened to be on.
        var placesFirst = inBody && gesture is MouseGesture.DoubleClick or MouseGesture.RightClick or MouseGesture.CtrlClick;

        return placesFirst ? [new PointerPlaced(column, hit.Row, context.Width, action)] : [action];
    }

    // The wheel over an open help, messages or context menu acts on that overlay wherever the pointer is on the screen.
    private static IReadOnlyList<Command> Wheeled(
        MouseContext context,
        MouseTarget target,
        int column,
        int row,
        MouseGesture gesture,
        DateTimeOffset at)
    {
        var inside = column >= 0 && row >= 0 && column < context.Width && row < context.Height;

        return inside && DefaultMouseMap.Find(target, gesture) is { } found && WiredParts.Contains(found.Part)
            ? [new ActionRequested(found.Action, found.Count, at)]
            : _none;
    }

    /// <summary>
    /// The tab chip under the pointer as a command: the tab of the chip (its close <c>x</c> included), null on a
    /// separator, a hidden-tab mark, anywhere else, or while an overlay is open.
    /// </summary>
    public static TabHovered Hover(MouseContext context, int column, int row)
    {
        if (MouseHitTester.Locate(context, column, row) is not { Target: MouseTarget.Tabs or MouseTarget.TabClose } hit
            || AreaAt(context, hit.Column) is not { Overflow: false } area)
        {
            return new TabHovered(null);
        }

        return new TabHovered(area.Tab);
    }

    /// <summary>
    /// A drag from one cell to another: on the columns layout a card dropped on another column. Anything that did not
    /// start on the body, did not move, or happens while an overlay is open does nothing; the reducer decides whether
    /// the drop is a transition (the same rules as the key).
    /// </summary>
    public static IReadOnlyList<Command> ResolveDrag(
        MouseContext context,
        int fromColumn,
        int fromRow,
        int toColumn,
        int toRow,
        DateTimeOffset at = default,
        bool live = false)
    {
        var gesture = context.ColumnsLayout ? MouseGesture.Drag : MouseGesture.DragRows;

        if (context.OverlayOpen || (fromColumn == toColumn && fromRow == toRow)
            || MouseHitTester.Locate(context, fromColumn, fromRow) is not { Target: MouseTarget.Lists } start
            || MouseHitTester.Locate(context, toColumn, toRow) is not { Target: MouseTarget.Lists } end
            || DefaultMouseMap.Find(MouseTarget.Lists, gesture) is not { } found
            || !WiredParts.Contains(found.Part))
        {
            return _none;
        }

        if (gesture == MouseGesture.DragRows)
        {
            return [new RowsDragged(fromColumn, start.Row, end.Row, context.Width, at, live)];
        }

        return [new CardDragged(fromColumn, start.Row, toColumn, context.Width)];
    }

    /// <summary>
    /// The held left button moved over rows of the list or backlog layout: the live range. The first move starts
    /// Visual mode on the pressed row even when the pointer already left the body (then the range is that row alone);
    /// later moves outside the body do nothing here, the auto-scroll step moves the cursor. Columns layout, overlays
    /// and a press outside the body do nothing.
    /// </summary>
    public static IReadOnlyList<Command> ResolveDragMove(
        MouseContext context,
        int fromColumn,
        int fromRow,
        int toColumn,
        int toRow,
        bool first)
    {
        if (LiveStart(context, fromColumn, fromRow) is not { } start)
        {
            return _none;
        }

        var end = start.Row;

        if (MouseHitTester.Locate(context, toColumn, toRow) is { Target: MouseTarget.Lists } inside)
        {
            end = inside.Row;
        }
        else if (!first)
        {
            return _none;
        }

        return [new RowsDragging(fromColumn, start.Row, end, context.Width, first)];
    }

    /// <summary>
    /// The auto-scroll speed of a held button whose pointer is on that screen row: signed rows per step, zero inside
    /// the body or when the press does not start a live range (columns layout, overlay, press outside the body).
    /// </summary>
    public static int DragScrollStep(MouseContext context, int fromColumn, int fromRow, int pointerRow)
    {
        return LiveStart(context, fromColumn, fromRow) is null ? 0 : DragAutoScroll.StepFor(context, pointerRow);
    }

    // The body row a live range starts on, null when this press cannot start one.
    private static MouseHit? LiveStart(MouseContext context, int fromColumn, int fromRow)
    {
        if (context.OverlayOpen || context.ColumnsLayout || context.DetailOpen
            || DefaultMouseMap.Find(MouseTarget.Lists, MouseGesture.DragRows) is not { } found
            || !WiredParts.Contains(found.Part))
        {
            return null;
        }

        return MouseHitTester.Locate(context, fromColumn, fromRow) is { Target: MouseTarget.Lists } start ? start : null;
    }

    // A row of an open overlay (picker, jump prompt, profile list, form field, suggestion popup): the row is chosen,
    // then the key's handler runs.
    private static IReadOnlyList<Command> OverlayClick(MouseHit hit, MouseGesture gesture, DateTimeOffset at)
    {
        if (hit.Target == MouseTarget.ContextMenu)
        {
            // The rows of this target differ by action: the reducer takes the action of the clicked menu item.
            return gesture == MouseGesture.Click
                ? [new OverlayRowClicked(hit.Target, hit.Row, ActionIds.FormNext, at)]
                : _none;
        }

        if (DefaultMouseMap.Find(hit.Target, gesture) is not { } found || !WiredParts.Contains(found.Part))
        {
            return _none;
        }

        return [new OverlayRowClicked(hit.Target, hit.Row, found.Action, at)];
    }

    private static bool IsOverlayRow(MouseTarget target)
    {
        return target is MouseTarget.PickerRow or MouseTarget.JumpRow or MouseTarget.ProfileRow
            or MouseTarget.SettingsCategory or MouseTarget.SettingsRow or MouseTarget.SettingsChoice
            or MouseTarget.ProfileAddButton or MouseTarget.ProfileEditButton or MouseTarget.ProfileTestButton
            or MouseTarget.ProfileSaveButton or MouseTarget.ProfileCancelButton
            or MouseTarget.FormField or MouseTarget.FormChoice or MouseTarget.EditorChoice or MouseTarget.ContextMenu
            or MouseTarget.ConfirmYes or MouseTarget.ConfirmNo or MouseTarget.PaletteRow or MouseTarget.HelpPage;
    }

    private static IReadOnlyList<Command> TabClick(MouseContext context, MouseHit hit, MouseGesture gesture, DateTimeOffset at)
    {
        if (gesture is not (MouseGesture.Click or MouseGesture.RightClick or MouseGesture.MiddleClick)
            || AreaAt(context, hit.Column) is not { } area)
        {
            return _none;
        }

        // A middle click closes the chip under the pointer, active or not, the way the x does (never a hidden tab).
        if (gesture == MouseGesture.MiddleClick)
        {
            return area.Overflow ? _none : [new TabClosed(area.Tab)];
        }

        // A right-click switches to the tab first, so the menu acts on the tab under the pointer.
        return gesture == MouseGesture.Click
            ? [new TabActivated(area.Tab)]
            : [new TabActivated(area.Tab), new ActionRequested(ActionIds.ViewMenuOpen, 1, at)];
    }

    /// <summary>The tab chip under a tab bar column, null on a separator or empty bar.</summary>
    private static TabArea? AreaAt(MouseContext context, int column)
    {
        foreach (var area in context.TabAreas)
        {
            if (column >= area.Start && column < area.End)
            {
                return area;
            }
        }

        return null;
    }
}
