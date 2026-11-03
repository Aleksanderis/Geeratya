namespace Geeratya.Core.Mouse;

/// <summary>What the pointer did, in terms independent of the terminal toolkit.</summary>
public enum MouseGesture
{
    Click,
    DoubleClick,
    RightClick,

    /// <summary>The middle button (wheel press) clicked.</summary>
    MiddleClick,
    CtrlClick,
    WheelUp,
    WheelDown,
    Drag,

    /// <summary>A drag over rows of a list or backlog (Visual mode); the same press-move-release as <see cref="Drag"/>.</summary>
    DragRows,

    /// <summary>
    /// Rows dragged from one section of a Scrum backlog onto the header or a row of a sprint section. The reducer
    /// decides between this and <see cref="DragRows"/> from where the release lands, so it has no resolver path.
    /// </summary>
    DragToSprint,

    /// <summary>As <see cref="DragToSprint"/>, released on the backlog section.</summary>
    DragToBacklog,
}
