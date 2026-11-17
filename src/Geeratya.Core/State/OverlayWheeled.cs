using Geeratya.Core.Mouse;

namespace Geeratya.Core.State;

/// <summary>
/// A wheel notch over an open overlay with a highlight (picker, Jump, palette, Settings, profile list, form and
/// comment popups): the highlight moves by <paramref name="Rows"/> (negative is up) and nothing runs.
/// </summary>
public sealed record OverlayWheeled(MouseTarget Target, int Rows) : Command;
