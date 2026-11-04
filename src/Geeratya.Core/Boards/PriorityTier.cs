namespace Geeratya.Core.Boards;

/// <summary>The three tiers a Jira priority name is drawn as, or none when the name is not recognised.</summary>
public enum PriorityTier
{
    /// <summary>No priority, or a name that matches no tier (drawn without a glyph rather than guessed).</summary>
    None,

    /// <summary>High and above (Highest, High, Critical, Blocker).</summary>
    High,

    /// <summary>The middle tier (Medium, Major).</summary>
    Medium,

    /// <summary>Low and below (Low, Lowest, Minor, Trivial).</summary>
    Low,
}
