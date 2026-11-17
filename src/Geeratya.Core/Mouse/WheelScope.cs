namespace Geeratya.Core.Mouse;

/// <summary>
/// Where the pointer must be for a wheel notch to act on the open overlay (decisions 3 and 7 of
/// <c>docs/internal/ui-consistency.md</c>): the wheel acts on what is under the pointer.
/// </summary>
public enum WheelScope
{
    /// <summary>Anywhere on the screen (help, messages and the context menu have no other wheel target).</summary>
    Screen,

    /// <summary>
    /// Anywhere over the overlay's box: its prompt, rows and footer alike (picker, Jump, palette, Settings, profiles).
    /// </summary>
    Box,

    /// <summary>
    /// Only over the rows of the overlay's popup list; the rest of the form takes no wheel (form and comment popups).
    /// </summary>
    Rows,
}
