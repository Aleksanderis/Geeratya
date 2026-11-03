namespace Geeratya.Core.Views;

/// <summary>The name a user gives a tab (<c>tab.rename</c>): one line, trimmed, at most <see cref="MaxLength"/> columns of text.</summary>
public static class TabNames
{
    /// <summary>A longer name is cut.</summary>
    public const int MaxLength = 40;

    /// <summary>The name to keep, or null when nothing is left (the tab then shows its source label).</summary>
    public static string? Clean(string? name)
    {
        if (name is null)
        {
            return null;
        }

        var line = string.Concat(name.Select(ch => char.IsControl(ch) ? ' ' : ch)).Trim();

        if (line.Length > MaxLength)
        {
            var end = char.IsHighSurrogate(line[MaxLength - 1]) ? MaxLength - 1 : MaxLength;

            line = line[..end].TrimEnd();
        }

        return line.Length == 0 ? null : line;
    }
}
