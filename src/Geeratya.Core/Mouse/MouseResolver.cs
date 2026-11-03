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
        int toRow)
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
            return [new RowsDragged(fromColumn, start.Row, end.Row, context.Width)];
        }

        return [new CardDragged(fromColumn, start.Row, toColumn, context.Width)];
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
            or MouseTarget.FormField or MouseTarget.FormChoice or MouseTarget.EditorChoice or MouseTarget.ContextMenu
            or MouseTarget.ConfirmYes or MouseTarget.ConfirmNo or MouseTarget.PaletteRow or MouseTarget.HelpPage;
    }

    private static IReadOnlyList<Command> TabClick(MouseContext context, MouseHit hit, MouseGesture gesture, DateTimeOffset at)
    {
        if (gesture is not (MouseGesture.Click or MouseGesture.RightClick))
        {
            return _none;
        }

        foreach (var area in context.TabAreas)
        {
            if (hit.Column >= area.Start && hit.Column < area.End)
            {
                // A right-click switches to the tab first, so the menu acts on the tab under the pointer.
                return gesture == MouseGesture.Click
                    ? [new TabActivated(area.Tab)]
                    : [new TabActivated(area.Tab), new ActionRequested(ActionIds.ViewMenuOpen, 1, at)];
            }
        }

        return _none;
    }
}
