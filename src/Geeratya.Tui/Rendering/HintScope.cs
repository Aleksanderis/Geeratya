namespace Geeratya.Tui.Rendering;

/// <summary>
/// Which screen of the active tab a hint belongs to; <see cref="Global"/> hints show everywhere in their mode.
/// </summary>
public enum HintScope
{
    Global,
    List,
    ListOnBoard,

    /// <summary>The Scrum backlog: a list whose sections are panes (<c>] ]</c> jumps between them).</summary>
    Backlog,
    Columns,

    /// <summary>The columns layout with the cursor on a card that lists sub-tasks (or on one of them).</summary>
    ColumnsSubtasks,
    Detail,
}
