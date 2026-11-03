namespace Geeratya.Core.Mouse;

/// <summary>
/// One row of the mouse table: <see cref="Gesture"/> on <see cref="Target"/> runs <see cref="Action"/>.
/// </summary>
/// <param name="Target">The area the pointer is over.</param>
/// <param name="Gesture">What the pointer did.</param>
/// <param name="Action">The action id a key would produce; the same handler runs it.</param>
/// <param name="Count">How many times the action repeats (a wheel notch moves 3 rows, like <c>3j</c>); 0 for <c>selection.mark</c> means "in place": the pointer marks the row and the cursor stays.</param>
/// <param name="Part">The part that wires the row: A (input layer), B (view interaction), C (writes, menus),
/// O (overlays: pickers, forms, popups, profile list).</param>
public sealed record MouseRow(MouseTarget Target, MouseGesture Gesture, string Action, int Count, char Part);
