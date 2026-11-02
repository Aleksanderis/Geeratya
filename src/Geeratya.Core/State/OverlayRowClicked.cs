using Geeratya.Core.Mouse;

namespace Geeratya.Core.State;

/// <summary>
/// A gesture on a row of an open overlay (picker, jump prompt, profile list, form field, suggestion popup): the row
/// is chosen, then <paramref name="Action"/> runs the handler its key would (Enter takes the row).
/// </summary>
public sealed record OverlayRowClicked(MouseTarget Target, int Index, string Action, DateTimeOffset At = default)
    : Command;
