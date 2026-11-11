using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Tui.Input;
using Geeratya.Tui.Rendering;
using Terminal.Gui.Input;

using AppCommand = Geeratya.Core.State.Command;

namespace Geeratya.Tui;

public sealed partial class ShellHost
{
    /// <summary>A pointer event: hit test and mouse table decide; the reducer runs the action like a key.</summary>
    private void OnMouse(object? sender, Mouse mouse)
    {
        var position = mouse.ScreenPosition;

        if (_dragTracker.TrackMotion(mouse.Flags, position.X, position.Y) is { } motion)
        {
            OnDragMoved(mouse, motion);

            return;
        }

        if (MouseFlagTranslator.IsHover(mouse.Flags))
        {
            OnHover(position.X, position.Y);

            return;
        }

        var live = _dragTracker.Moving;
        var drag = _dragTracker.Track(mouse.Flags, position.X, position.Y);

        if (mouse.Flags.HasFlag(MouseFlags.LeftButtonReleased))
        {
            StopDragScroll();
        }

        if (drag is null && (_dragTracker.SwallowsClick(mouse.Flags)
            || !MouseFlagTranslator.TryTranslate(mouse.Flags, out _)))
        {
            return;
        }

        var now = TimeProvider.System.GetUtcNow();
        var wheel = mouse.Flags.HasFlag(MouseFlags.WheeledUp) || mouse.Flags.HasFlag(MouseFlags.WheeledDown);
        var context = PointerContext(now, wheel);
        var commands = ResolvePointer(context, drag, live, mouse.Flags, new MouseCell(position.X, position.Y), now);

        if (commands.Count == 0)
        {
            return;
        }

        mouse.Handled = true;

        foreach (var command in commands)
        {
            Dispatch(command);
        }
    }

    // A pointer move: only the tab row cares, so the context is built only over it or to clear a hover.
    private void OnHover(int column, int row)
    {
        if (row != 0 && _state.HoveredTab is null)
        {
            return;
        }

        var hovered = MouseResolver.Hover(PointerContext(TimeProvider.System.GetUtcNow()), column, row);

        if (hovered.Tab != _state.HoveredTab)
        {
            Dispatch(hovered);
        }
    }

    // A wheel notch outside an overlay needs no drawn cells, so a burst of notches builds no frame (#933); anything
    // else reads the regions and the marker, which builds the frame of the latest state once.
    private MouseContext PointerContext(DateTimeOffset now, bool wheel = false)
    {
        var size = _application!.Screen.Size;

        var context = MouseContext.From(_state, size.Width, size.Height, FrameBuilder.HeaderRows, now, null, _keymap)
            with
            {
                LaneChip = StatusBar.LaneCells(_state, size.Width, _keymap, now),
            };

        if (wheel && !context.OverlayOpen)
        {
            return context;
        }

        return context with { Regions = _screen!.OverlayRegions, RowMenuMarker = _screen.RowMenuMarker };
    }

    // The held button moved: the range follows the pointer; outside the body a timer keeps moving the cursor.
    private void OnDragMoved(Mouse mouse, MouseDragMotion motion)
    {
        var context = PointerContext(TimeProvider.System.GetUtcNow());
        var commands = MouseResolver.ResolveDragMove(
            context,
            motion.FromColumn,
            motion.FromRow,
            motion.Column,
            motion.Row,
            motion.First);

        if (commands.Count > 0)
        {
            mouse.Handled = true;
        }

        foreach (var command in commands)
        {
            Dispatch(command);
        }

        // The speed follows the pointer on every move, also when it is outside the body and nothing else resolves.
        _dragScroller.Start(MouseResolver.DragScrollStep(context, motion.FromColumn, motion.FromRow, motion.Row));

    }

    /// <summary>Timer thread: hands one auto-scroll step to the UI thread.</summary>
    private void OnDragScrollStep(object? sender, int rows)
    {
        if (!_shutdownToken.IsCancellationRequested)
        {
            InvokeOnUi(() => StepDragScroll(rows));
        }
    }

    // On the UI thread: a step queued before the release must not move the cursor after it.
    private void StepDragScroll(int rows)
    {
        if (_dragTracker.Held)
        {
            Dispatch(new RowsDragScrolled(rows));
        }
    }

    private void StopDragScroll()
    {
        _dragScroller.Stop();
    }

    private static IReadOnlyList<AppCommand> ResolvePointer(
        MouseContext context,
        MouseDrag? drag,
        bool live,
        MouseFlags flags,
        MouseCell position,
        DateTimeOffset now)
    {
        if (drag is { } dropped)
        {
            return MouseResolver.ResolveDrag(
                context,
                dropped.FromColumn,
                dropped.FromRow,
                dropped.ToColumn,
                dropped.ToRow,
                now,
                live);
        }

        return MouseFlagTranslator.TryTranslate(flags, out var gesture)
            ? MouseResolver.Resolve(context, position.Column, position.Row, gesture, now)
            : [];
    }

    /// <summary>Bracketed paste arrives as one event, not as keys; the focused view must not also receive it.</summary>
    private void OnPaste(object? sender, PasteEventArgs e)
    {
        e.Handled = true;

        if (_pendingEdit is null)
        {
            Dispatch(new TextPasted(e.Text));
        }
    }
}
