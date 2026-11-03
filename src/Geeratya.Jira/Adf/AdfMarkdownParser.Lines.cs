
namespace Geeratya.Jira.Adf;

public static partial class AdfMarkdownParser
{
    private static bool StartsBlock(string line)
    {
        var start = LeadingWhitespace(line, out var columns);

        if (columns > MaxIndent)
        {
            return false;
        }

        var content = line[start..];

        return IsFenceStart(content, out _)
            || IsHeading(content, out _)
            || IsRule(content)
            || content[0] == AdfMarkdownSyntax.QuoteMarker
            || TryMarker(line, out _);
    }

    private static bool IsFenceStart(string content, out int length)
    {
        length = 0;

        while (length < content.Length && content[length] == AdfMarkdownSyntax.CodeFence)
        {
            length++;
        }

        return length >= AdfMarkdownSyntax.MinFenceLength
            && !content.AsSpan(length).Contains(AdfMarkdownSyntax.CodeFence);
    }

    private static bool IsFenceEnd(string line, int fenceLength)
    {
        var start = LeadingWhitespace(line, out var columns);

        if (columns > MaxIndent)
        {
            return false;
        }

        var length = 0;

        while (start + length < line.Length && line[start + length] == AdfMarkdownSyntax.CodeFence)
        {
            length++;
        }

        return length >= fenceLength && IsBlank(line[(start + length)..]);
    }

    private static bool IsHeading(string content, out int level)
    {
        level = 0;

        while (level < content.Length && content[level] == AdfMarkdownSyntax.HeadingMarker)
        {
            level++;
        }

        return level is >= 1 and <= AdfMarkdownSyntax.MaxHeadingLevel
            && (level == content.Length || content[level] == ' ');
    }

    private static bool IsRule(string content)
    {
        var trimmed = content.TrimEnd();

        return trimmed.Length >= MinRuleLength
            && RuleCharacters.Contains(trimmed[0])
            && trimmed.All(character => character == trimmed[0]);
    }

    private static bool IsBlank(string line)
    {
        return string.IsNullOrWhiteSpace(line);
    }

    private static int NextNonBlank(IReadOnlyList<string> lines, int start)
    {
        var index = start;

        while (index < lines.Count && IsBlank(lines[index]))
        {
            index++;
        }

        return index;
    }

    /// <summary>
    /// Returns the index of the first non-blank character and its column (a tab runs to a multiple of 4).
    /// </summary>
    private static int LeadingWhitespace(string line, out int columns)
    {
        columns = 0;

        var index = 0;

        while (index < line.Length && line[index] is ' ' or '\t')
        {
            columns += line[index] == '\t' ? AdfMarkdownSyntax.TabWidth - columns % AdfMarkdownSyntax.TabWidth : 1;
            index++;
        }

        return index;
    }

    private static string StripColumns(string line, int count)
    {
        var columns = 0;
        var index = 0;

        while (index < line.Length && columns < count && line[index] is ' ' or '\t')
        {
            columns += line[index] == '\t' ? AdfMarkdownSyntax.TabWidth - columns % AdfMarkdownSyntax.TabWidth : 1;
            index++;
        }

        return line[index..];
    }
}
