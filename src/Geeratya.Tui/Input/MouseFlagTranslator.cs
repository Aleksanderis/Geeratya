using Geeratya.Core.Mouse;
using Terminal.Gui.Input;

namespace Geeratya.Tui.Input;

/// <summary>Turns Terminal.Gui mouse flags into the <see cref="MouseGesture"/> of the mouse table.</summary>
public static class MouseFlagTranslator
{
    /// <summary>True for a pointer move with no button held (the hover of a tab chip).</summary>
    public static bool IsHover(MouseFlags flags)
    {
        const MouseFlags buttons = MouseFlags.LeftButtonPressed | MouseFlags.MiddleButtonPressed
            | MouseFlags.RightButtonPressed;

        return flags.HasFlag(MouseFlags.PositionReport) && (flags & buttons) == 0;
    }

    /// <summary>False for events the table ignores (moves, presses, releases, a sideways wheel).</summary>
    public static bool TryTranslate(MouseFlags flags, out MouseGesture gesture)
    {
        gesture = default;

        var wheel = flags.HasFlag(MouseFlags.WheeledUp) || flags.HasFlag(MouseFlags.WheeledDown);

        if (flags.HasFlag(MouseFlags.Shift) && wheel)
        {
            return false;
        }

        if (flags.HasFlag(MouseFlags.WheeledUp))
        {
            gesture = MouseGesture.WheelUp;
        }
        else if (flags.HasFlag(MouseFlags.WheeledDown))
        {
            gesture = MouseGesture.WheelDown;
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonDoubleClicked))
        {
            gesture = MouseGesture.DoubleClick;
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonClicked) && flags.HasFlag(MouseFlags.Ctrl))
        {
            gesture = MouseGesture.CtrlClick;
        }
        else if (flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            gesture = MouseGesture.Click;
        }
        else if (flags.HasFlag(MouseFlags.MiddleButtonClicked))
        {
            gesture = MouseGesture.MiddleClick;
        }
        else if (flags.HasFlag(MouseFlags.RightButtonClicked))
        {
            gesture = MouseGesture.RightClick;
        }
        else
        {
            return false;
        }

        return true;
    }
}
