using Terminal.Gui.Input;

namespace Geeratya.Tui.Input;

/// <summary>
/// Pairs the press and the release of the left button into a drag. A drag is a release on another cell than the press;
/// a release on the same cell is left to the click events. The click the toolkit may still send after a drag is
/// swallowed once, so a drop never also selects or opens a row.
/// </summary>
public sealed class MouseDragTracker
{
    private const MouseFlags ClickFlags = MouseFlags.LeftButtonClicked | MouseFlags.LeftButtonDoubleClicked;

    private MouseCell? _start;
    private bool _dragged;
    private bool _moved;

    /// <summary>True from the first motion of the held button until the release: a live range is on screen.</summary>
    public bool Moving => _moved;

    /// <summary>True while the left button is held after a press.</summary>
    public bool Held => _start is not null;

    /// <summary>
    /// The motion a held-button position report makes, null for any other event, for motion on the press cell before
    /// the first move, and when no press was seen.
    /// </summary>
    public MouseDragMotion? TrackMotion(MouseFlags flags, int column, int row)
    {
        if (!flags.HasFlag(MouseFlags.LeftButtonPressed) || !flags.HasFlag(MouseFlags.PositionReport)
            || _start is not { } start || (!_moved && start.Column == column && start.Row == row))
        {
            return null;
        }

        var first = !_moved;

        _moved = true;

        return new MouseDragMotion(start.Column, start.Row, column, row, first);
    }

    /// <summary>The drag a button event completes, null for every other event.</summary>
    public MouseDrag? Track(MouseFlags flags, int column, int row)
    {
        if (flags.HasFlag(MouseFlags.LeftButtonPressed) && flags.HasFlag(MouseFlags.PositionReport))
        {
            // Terminal.Gui reports the motion of a held button as Pressed plus PositionReport: not a new press.
            return null;
        }

        if (flags.HasFlag(MouseFlags.LeftButtonPressed))
        {
            _start = new MouseCell(column, row);
            _dragged = false;
            _moved = false;

            return null;
        }

        if (!flags.HasFlag(MouseFlags.LeftButtonReleased) || _start is not { } start)
        {
            return null;
        }

        _start = null;
        _moved = false;

        if (start.Column == column && start.Row == row)
        {
            return null;
        }

        _dragged = true;

        return new MouseDrag(start.Column, start.Row, column, row);
    }

    /// <summary>True once for a click event that follows a drag: it must not act.</summary>
    public bool SwallowsClick(MouseFlags flags)
    {
        if (!_dragged || (flags & ClickFlags) == 0)
        {
            return false;
        }

        _dragged = false;

        return true;
    }
}
