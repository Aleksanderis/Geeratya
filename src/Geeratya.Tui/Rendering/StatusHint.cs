using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// One key hint of the status bar: the key as the keymap spells it, a short label and the action it names.
/// </summary>
public sealed record StatusHint(InputMode Mode, string Key, string Label, string ActionId);
