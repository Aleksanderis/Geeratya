namespace Geeratya.Core.Boards;

/// <summary>
/// Maps the name of a Jira priority to its <see cref="PriorityTier"/> (the default scheme and the classic one).
/// </summary>
public static class PriorityTierOf
{
    /// <summary>The tier of <paramref name="name"/>; <see cref="PriorityTier.None"/> for a blank or unknown name.</summary>
    public static PriorityTier Name(string? name)
    {
        var text = name?.Trim() ?? string.Empty;

        if (text.Length == 0)
        {
            return PriorityTier.None;
        }

        if (Is(text, "blocker") || Is(text, "critical") || Is(text, "highest") || Is(text, "high"))
        {
            return PriorityTier.High;
        }

        if (Is(text, "medium") || Is(text, "major"))
        {
            return PriorityTier.Medium;
        }

        return Is(text, "low") || Is(text, "lowest") || Is(text, "minor") || Is(text, "trivial")
            ? PriorityTier.Low
            : PriorityTier.None;
    }

    private static bool Is(string text, string word)
    {
        return text.Equals(word, StringComparison.OrdinalIgnoreCase)
            || text.EndsWith($" {word}", StringComparison.OrdinalIgnoreCase);
    }
}
