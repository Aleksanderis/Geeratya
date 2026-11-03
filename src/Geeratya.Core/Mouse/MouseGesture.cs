namespace Geeratya.Core.Mouse;

/// <summary>What the pointer did, in terms independent of the terminal toolkit.</summary>
public enum MouseGesture
{
    Click,
    DoubleClick,
    RightClick,
    CtrlClick,
    WheelUp,
    WheelDown,
    Drag,

    /// <summary>A drag over rows of a list or backlog (Visual mode); the same press-move-release as <see cref="Drag"/>.</summary>
    DragRows,
}
