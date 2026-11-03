namespace Geeratya.Core.Keymap;

/// <summary>
/// How much of the command set the lists that teach keys show (help, palette, which-key, status hints). A level
/// hides, it never disables: an action above the level keeps its key and still runs.
/// </summary>
public enum CommandLevel
{
    /// <summary>What a normal working day needs.</summary>
    Everyday,

    /// <summary>Everyday plus the rare or risky actions (delete, unlink, rank to the top, bottom or a place).</summary>
    Advanced,
}
