namespace Geeratya.Core.Keymap;

/// <summary>
/// Where an action applies: the layouts of a view, or everywhere. Geeratya keeps the same keys in every view, so this
/// tag (not a per-view key row) tells the help overlay which actions belong on the page of the active layout.
/// </summary>
[Flags]
public enum ActionScope
{
    /// <summary>Applies everywhere (tabs, palette, help, quit); listed on the Global help page.</summary>
    Global = 1,

    /// <summary>The list layout of a view.</summary>
    List = 2,

    /// <summary>The columns layout of a board.</summary>
    Columns = 4,

    /// <summary>An opened issue.</summary>
    Detail = 8,

    /// <summary>The backlog view.</summary>
    Backlog = 16,

    /// <summary>Every layout but not the global page.</summary>
    Views = List | Columns | Detail | Backlog,

    /// <summary>The layouts that show a list of issues.</summary>
    Lists = List | Columns | Backlog,
}
