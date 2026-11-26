using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Mouse;

/// <summary>Resolves a pointer event to the commands the state layer runs: hit test, then the mouse table.</summary>
public static class MouseResolver
{
    /// <summary>The parts of the mouse table that are wired today; rows of later parts have no effect yet.</summary>
    public const string WiredParts = "ABOMVT";

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

        // Only a plain click belongs to the marker; any other gesture on its cells acts on the row like anywhere on it.
        if (hit.Target == MouseTarget.RowMenuButton && gesture != MouseGesture.Click)
        {
            hit = hit with { Target = context.DetailOpen ? MouseTarget.Detail : MouseTarget.Lists };
        }

        if (hit.Target == MouseTarget.Backdrop
            && context is { DetailOpen: true, OverlayOpen: false, ActiveTab: { } tab })
        {
            if (gesture == MouseGesture.Click)
            {
                return [new IssueDetailCloseRequested(tab)];
            }

            // The wheel over the backdrop still scrolls the detail, as it did when the detail filled the body.
            if (gesture is not (MouseGesture.WheelUp or MouseGesture.WheelDown))
            {
                return _none;
            }

            hit = hit with { Target = MouseTarget.Detail };
        }

        if (hit.Target == MouseTarget.EditorText)
        {
            return TextClick(context, hit, column, gesture);
        }

        if (IsOverlayRow(hit.Target))
        {
            // A wheel notch over an overlay never gets here: the open overlay's wheel target took it above.
            return gesture is MouseGesture.WheelUp or MouseGesture.WheelDown ? _none : OverlayClick(hit, gesture, at);
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

        // The wheel scrolls the pane under the pointer, not the one the cursor happens to be in.
        if (hit.Target == MouseTarget.Detail && gesture is MouseGesture.WheelUp or MouseGesture.WheelDown)
        {
            return [new DetailWheeled(column, hit.Row, action)];
        }

        // The row under the pointer is the target of the action, not the row the cursor happened to be on.
        var placesFirst = inBody && gesture is MouseGesture.DoubleClick or MouseGesture.RightClick or MouseGesture.CtrlClick;

        return placesFirst ? [new PointerPlaced(column, hit.Row, context.Width, action)] : [action];
    }

    // The wheel acts on what is under the pointer (decisions 3 and 7 of the UI consistency audit): help, messages and
    // the context menu take it anywhere; the other overlays over their box or, for a popup, over its rows only.
    private static IReadOnlyList<Command> Wheeled(
        MouseContext context,
        MouseTarget target,
        int column,
        int row,
        MouseGesture gesture,
        DateTimeOffset at)
    {
        if (!WheelReaches(context, target, column, row)
            || DefaultMouseMap.Find(target, gesture) is not { } found
            || !WiredParts.Contains(found.Part))
        {
            return _none;
        }

        if (context.WheelScope == WheelScope.Screen)
        {
            return [new ActionRequested(found.Action, found.Count, at)];
        }

        return [new OverlayWheeled(target, gesture == MouseGesture.WheelDown ? found.Count : -found.Count)];
    }

    private static bool WheelReaches(MouseContext context, MouseTarget target, int column, int row)
    {
        if (context.WheelScope == WheelScope.Screen)
        {
            return column >= 0 && row >= 0 && column < context.Width && row < context.Height;
        }

        if (MouseHitTester.Locate(context, column, row) is not { } hit)
        {
            return false;
        }

        return context.WheelScope == WheelScope.Rows ? hit.Target == target : hit.Target != MouseTarget.Backdrop;
    }

    /// <summary>
    /// The tab chip under the pointer as a command: the tab of the chip (its close <c>x</c> included), null on a
    /// separator, a hidden-tab mark, anywhere else, while an overlay is open, or under <see cref="TabBar.CloseFrom"/>
    /// columns (no <c>x</c> is drawn there, so a hover would redraw for nothing).
    /// </summary>
    public static TabHovered Hover(MouseContext context, int column, int row)
    {
        if (context.Width < TabBar.CloseFrom
            || MouseHitTester.Locate(context, column, row) is not { Target: MouseTarget.Tabs or MouseTarget.TabClose } hit
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
        if (TextDrag(context, fromColumn, fromRow, toColumn, toRow, live) is { } textDrag)
        {
            return [textDrag];
        }

        if (DetailDrag(context, fromColumn, fromRow, toColumn, toRow) is { } detailDrag)
        {
            return [detailDrag];
        }

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
        if (TextDrag(context, fromColumn, fromRow, toColumn, toRow, !first) is { } textDrag)
        {
            return [textDrag];
        }

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

    // A click or double-click on the text being edited: the caret or the word under the pointer.
    private static IReadOnlyList<Command> TextClick(
        MouseContext context,
        MouseHit hit,
        int column,
        MouseGesture gesture)
    {
        if (gesture is not (MouseGesture.Click or MouseGesture.DoubleClick)
            || hit.Region is not { } region
            || DefaultMouseMap.Find(MouseTarget.EditorText, gesture) is not { } found
            || !WiredParts.Contains(found.Part))
        {
            return _none;
        }

        var word = gesture == MouseGesture.DoubleClick;

        var top = TopOf(context, region);

        return [new TextPointed(region.Index, region.Line, column - region.Left, region.Width, word, top)];
    }

    // A drag that starts on the text being edited and is now over a row of the same text. A pointer outside the text
    // changes nothing once the range exists; on the first move it still places the caret on the press cell, so a
    // selection made earlier by keys is dropped and the next move starts from the press.
    private static TextDragged? TextDrag(
        MouseContext context,
        int fromColumn,
        int fromRow,
        int toColumn,
        int toRow,
        bool continued)
    {
        if (!context.OverlayOpen || DefaultMouseMap.Find(MouseTarget.EditorText, MouseGesture.DragRows) is not { } found
            || !WiredParts.Contains(found.Part)
            || MouseHitTester.Locate(context, fromColumn, fromRow) is not
            {
                Target: MouseTarget.EditorText,
                Region: { } start,
            })
        {
            return null;
        }

        var end = TextRowAt(context, toColumn, toRow, start.Index);

        if (end is null && continued)
        {
            return null;
        }

        return new TextDragged(
            start.Index,
            start.Line,
            fromColumn - start.Left,
            (end ?? start).Line,
            (end is null ? fromColumn : toColumn) - (end ?? start).Left,
            start.Width,
            continued,
            TopOf(context, start));
    }

    // The first line of the text of the field of <region> that the frame shows (a window of a long text).
    private static int TopOf(MouseContext context, OverlayRegion region)
    {
        return context.Regions
            .Where(candidate => candidate.Target == MouseTarget.EditorText && candidate.Index == region.Index)
            .Select(static candidate => candidate.Line)
            .DefaultIfEmpty(region.Line)
            .Min();
    }

    // The row of the text of field <index> under the cell, null anywhere else.
    private static OverlayRegion? TextRowAt(MouseContext context, int column, int row, int index)
    {
        if (MouseHitTester.Locate(context, column, row) is { Target: MouseTarget.EditorText, Region: { } region }
            && region.Index == index)
        {
            return region;
        }

        return null;
    }

    // A drag from one cell of the opened issue to another: select and copy the text between them (mouse table: Detail).
    private static DetailDragged? DetailDrag(MouseContext context, int fromColumn, int fromRow, int toColumn, int toRow)
    {
        if (context.OverlayOpen || !context.DetailOpen || (fromColumn == toColumn && fromRow == toRow)
            || DefaultMouseMap.Find(MouseTarget.Detail, MouseGesture.DragRows) is not { } found
            || !WiredParts.Contains(found.Part))
        {
            return null;
        }

        if (MouseHitTester.Locate(context, fromColumn, fromRow) is not { Target: MouseTarget.Detail } start
            || MouseHitTester.Locate(context, toColumn, toRow) is not { Target: MouseTarget.Detail } end)
        {
            return null;
        }

        return new DetailDragged(fromColumn, start.Row, toColumn, end.Row, context.Width);
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
            or MouseTarget.FormField or MouseTarget.FormChoice or MouseTarget.FormMoreFields
            or MouseTarget.EditorChoice or MouseTarget.ContextMenu
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
