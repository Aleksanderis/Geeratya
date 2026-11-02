namespace Geeratya.Tui.Rendering;

/// <summary>
/// Which screen of the active tab a hint belongs to; <see cref="Global"/> hints show everywhere in their mode.
/// </summary>
public enum HintScope
{
    Global,
    List,
    ListOnBoard,
    Columns,
    Detail,
}
