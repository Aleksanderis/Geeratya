using Geeratya.Jira.Issues;

namespace Geeratya.Core.Jump;

/// <summary>Reads what is typed in the jump prompt. Pure.</summary>
public static class JumpKey
{
    private const char KeySeparator = '-';

    /// <summary>
    /// <c>gcm-12</c> becomes <c>GCM-12</c>; a bare number (<c>12</c>) takes <paramref name="defaultProject"/>.
    /// </summary>
    public static JumpParse Parse(string text, string? defaultProject)
    {
        var trimmed = text.Trim();

        if (trimmed.Length == 0)
        {
            return new JumpParse(JumpParseKind.Empty, string.Empty);
        }

        if (trimmed.All(char.IsAsciiDigit))
        {
            return defaultProject is null
                ? new JumpParse(JumpParseKind.NeedsProject, string.Empty)
                : Checked($"{defaultProject}{KeySeparator}{trimmed}");
        }

        return Checked(trimmed);
    }

    /// <summary>The project part of a key (<c>GCM</c> of <c>GCM-12</c>), or null when it has none.</summary>
    public static string? ProjectOf(string key)
    {
        var index = key.LastIndexOf(KeySeparator);

        return index > 0 ? key[..index] : null;
    }

    private static JumpParse Checked(string candidate)
    {
        var key = candidate.ToUpperInvariant();

        return IssueUrls.IsValidKey(key)
            ? new JumpParse(JumpParseKind.Key, key)
            : new JumpParse(JumpParseKind.Invalid, string.Empty);
    }
}
