using Geeratya.Core.Keymap;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// One key hint of the status bar: a short label and the action it names; the key is read from the keymap.
/// </summary>
public sealed record StatusHint(InputMode Mode, string ActionId, string Label);
