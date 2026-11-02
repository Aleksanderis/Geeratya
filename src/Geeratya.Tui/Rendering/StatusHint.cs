using Geeratya.Core.Keymap;

namespace Geeratya.Tui.Rendering;

/// <summary>Which screen of the active tab a hint belongs to; <see cref="Global"/> hints show everywhere in their mode.</summary>
public enum HintScope
{
    Global,
    List,
    ListOnBoard,
    Columns,
    Detail,
}

/// <summary>
/// One key hint of the status bar: a short label and the action it names; the key is read from the keymap.
/// A hint with a <paramref name="SecondActionId"/> shows both keys as <c>j/k</c>; it is left out unless both are bound.
/// </summary>
public sealed record StatusHint(
    InputMode Mode,
    string ActionId,
    string Label,
    HintScope Scope = HintScope.Global,
    string? SecondActionId = null);
