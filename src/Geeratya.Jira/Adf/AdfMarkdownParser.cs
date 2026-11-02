using System.Globalization;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Reads Markdown-like text (<see cref="AdfMarkdownSyntax" />) into an <see cref="AdfDocument" />. Pure and
/// tolerant: any text gives a document and nothing throws. Blocks are separated by blank lines; a list, quote,
/// heading, rule or fence also starts at the next line without one. Nesting is by indentation under a list marker or
/// by <c>&gt;</c>, to at most <see cref="AdfMarkdownSyntax.MaxNesting" /> levels. A newline inside a paragraph is a
/// hard break.
/// </summary>
public static class AdfMarkdownParser
{
    private const int MaxIndent = 3;
    private const int MaxOrderDigits = 9;
    private const int DocumentVersion = 1;
    private const int MinRuleLength = 3;
    private const string RuleCharacters = "-*_";
    private const string BulletCharacters = "-*+";
    private const string TaskDoneUppercase = "[X] ";

    public static AdfDocument Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return AdfDocument.Empty;
        }

        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        return new AdfDocument(DocumentVersion, ParseBlocks(lines, 0), false);
    }

    private static List<AdfNode> ParseBlocks(IReadOnlyList<string> lines, int depth)
    {
        var blocks = new List<AdfNode>();
        var index = 0;

        while (index < lines.Count)
        {
            if (IsBlank(lines[index]))
            {
                index++;

                continue;
            }

            index = depth >= AdfMarkdownSyntax.MaxNesting
                ? ParseParagraph(lines, index, blocks)
                : ParseBlock(lines, index, depth, blocks);
        }

        return blocks;
    }

    private static int ParseBlock(IReadOnlyList<string> lines, int index, int depth, List<AdfNode> blocks)
    {
        var line = lines[index];
        var start = LeadingWhitespace(line, out var columns);

        if (columns > MaxIndent)
        {
            return ParseParagraph(lines, index, blocks);
        }

        var content = line[start..];

        if (IsFenceStart(content, out var fenceLength))
        {
            return ParseFence(lines, index, content, fenceLength, blocks);
        }

        if (IsHeading(content, out var level))
        {
            var heading = content[level..].Trim();

            blocks.Add(new AdfHeading(level, AdfMarkdownInlineParser.Parse(heading)));

            return index + 1;
        }

        if (IsRule(content))
        {
            blocks.Add(new AdfRule());

            return index + 1;
        }

        if (content[0] == AdfMarkdownSyntax.QuoteMarker)
        {
            return ParseQuote(lines, index, depth, blocks);
        }

        if (TryMarker(line, out var marker))
        {
            return ParseList(lines, index, depth, marker.IsOrdered, blocks);
        }

        return ParseParagraph(lines, index, blocks);
    }

    private static int ParseParagraph(IReadOnlyList<string> lines, int start, List<AdfNode> blocks)
    {
        var parts = new List<string> { lines[start].TrimStart() };
        var index = start + 1;

        while (index < lines.Count && !IsBlank(lines[index]) && !StartsBlock(lines[index]))
        {
            parts.Add(lines[index].TrimStart());
            index++;
        }

        var children = AdfMarkdownInlineParser.Parse(string.Join('\n', parts));

        if (children.Count > 0)
        {
            blocks.Add(new AdfParagraph(children));
        }

        return index;
    }

    private static int ParseFence(
        IReadOnlyList<string> lines,
        int start,
        string opening,
        int fenceLength,
        List<AdfNode> blocks)
    {
        var language = opening[fenceLength..].Trim();
        var code = new List<string>();
        var index = start + 1;

        while (index < lines.Count && !IsFenceEnd(lines[index], fenceLength))
        {
            code.Add(lines[index]);
            index++;
        }

        var text = string.Join('\n', code);
        IReadOnlyList<AdfNode> children = text.Length == 0 ? [] : [new AdfText(text, [])];

        blocks.Add(new AdfCodeBlock(language.Length == 0 ? null : language, children));

        return Math.Min(index + 1, lines.Count);
    }

    private static int ParseQuote(IReadOnlyList<string> lines, int start, int depth, List<AdfNode> blocks)
    {
        var inner = new List<string>();
        var index = start;

        while (index < lines.Count)
        {
            var line = lines[index];
            var offset = LeadingWhitespace(line, out var columns);

            if (columns > MaxIndent || offset >= line.Length || line[offset] != AdfMarkdownSyntax.QuoteMarker)
            {
                break;
            }

            var skip = offset + 1;

            if (skip < line.Length && line[skip] == ' ')
            {
                skip++;
            }

            inner.Add(line[skip..]);
            index++;
        }

        blocks.Add(new AdfBlockquote(ParseBlocks(inner, depth + 1)));

        return index;
    }

    private static int ParseList(
        IReadOnlyList<string> lines,
        int start,
        int depth,
        bool isOrdered,
        List<AdfNode> blocks)
    {
        var entries = new List<ListEntry>();
        var index = start;
        var firstNumber = 1;

        while (true)
        {
            TryMarker(lines[index], out var marker);

            if (entries.Count == 0)
            {
                firstNumber = marker.Number;
            }

            var content = new List<string> { marker.Remainder };

            index = CollectItem(lines, index + 1, marker, content);
            entries.Add(new ListEntry(content));

            var next = NextNonBlank(lines, index);

            if (next >= lines.Count || !TryMarker(lines[next], out var following) || following.IsOrdered != isOrdered)
            {
                break;
            }

            index = next;
        }

        blocks.Add(BuildList(entries, depth, isOrdered, firstNumber));

        return index;
    }

    private static int CollectItem(IReadOnlyList<string> lines, int start, ListMarker marker, List<string> content)
    {
        var index = start;

        while (index < lines.Count)
        {
            var line = lines[index];

            if (IsBlank(line))
            {
                var next = NextNonBlank(lines, index);

                if (next >= lines.Count || !BelongsToItem(lines[next], marker))
                {
                    break;
                }

                content.Add(string.Empty);
                index++;

                continue;
            }

            if (!BelongsToItem(line, marker))
            {
                break;
            }

            content.Add(StripColumns(line, marker.ContentColumns));
            index++;
        }

        return index;
    }

    private static bool BelongsToItem(string line, ListMarker marker)
    {
        LeadingWhitespace(line, out var columns);

        if (columns >= marker.ContentColumns)
        {
            return true;
        }

        // A nested list written with less indentation than the marker text still nests under the item.
        return columns > marker.MarkerColumns && TryMarker(line, out _);
    }

    private static AdfNode BuildList(List<ListEntry> entries, int depth, bool isOrdered, int firstNumber)
    {
        if (!isOrdered && TryBuildTaskList(entries, depth) is { } taskList)
        {
            return taskList;
        }

        var items = new List<AdfNode>(entries.Count);

        for (var index = 0; index < entries.Count; index++)
        {
            items.Add(new AdfListItem(ParseBlocks(entries[index].Content, depth + 1)));
        }

        return isOrdered ? new AdfOrderedList(firstNumber, items) : new AdfBulletList(items);
    }

    /// <summary>
    /// A bullet list is a task list when every item starts with <c>[ ]</c> or <c>[x]</c> and holds one paragraph at
    /// most (a task item cannot hold blocks); anything else stays a bullet list with the brackets as text.
    /// </summary>
    private static AdfTaskList? TryBuildTaskList(List<ListEntry> entries, int depth)
    {
        var items = new List<AdfNode>(entries.Count);

        for (var index = 0; index < entries.Count; index++)
        {
            var content = entries[index].Content;

            if (!TryTaskState(content[0], out var state, out var remainder))
            {
                return null;
            }

            var lines = new List<string>(content) { [0] = remainder };
            var blocks = ParseBlocks(lines, depth + 1);

            if (blocks.Count > 1 || (blocks.Count == 1 && blocks[0] is not AdfParagraph))
            {
                return null;
            }

            items.Add(new AdfTaskItem(state, blocks.Count == 0 ? [] : ((AdfParagraph)blocks[0]).Children));
        }

        return new AdfTaskList(items);
    }

    private static bool TryTaskState(string line, out AdfTaskState state, out string remainder)
    {
        // A task without text is just the bracket pair at the end of the line.
        var first = line.Length == AdfMarkdownSyntax.TaskTodoMarker.Length - 1 ? $"{line} " : line;

        state = AdfTaskState.Todo;
        remainder = string.Empty;

        if (first.StartsWith(AdfMarkdownSyntax.TaskTodoMarker, StringComparison.Ordinal))
        {
            remainder = first[AdfMarkdownSyntax.TaskTodoMarker.Length..];

            return true;
        }

        if (first.StartsWith(AdfMarkdownSyntax.TaskDoneMarker, StringComparison.Ordinal)
            || first.StartsWith(TaskDoneUppercase, StringComparison.Ordinal))
        {
            state = AdfTaskState.Done;
            remainder = first[AdfMarkdownSyntax.TaskDoneMarker.Length..];

            return true;
        }

        return false;
    }

    private static bool TryMarker(string line, out ListMarker marker)
    {
        marker = default;

        var start = LeadingWhitespace(line, out var columns);

        if (columns > MaxIndent || start >= line.Length)
        {
            return false;
        }

        var character = line[start];
        var isOrdered = false;
        var number = 1;
        var length = 1;

        if (char.IsAsciiDigit(character))
        {
            var digits = 0;

            while (start + digits < line.Length && char.IsAsciiDigit(line[start + digits]))
            {
                digits++;
            }

            var after = start + digits;

            if (digits > MaxOrderDigits || after >= line.Length || line[after] is not ('.' or ')'))
            {
                return false;
            }

            number = int.Parse(line.AsSpan(start, digits), NumberStyles.None, CultureInfo.InvariantCulture);
            isOrdered = true;
            length = digits + 1;
        }
        else if (!BulletCharacters.Contains(character))
        {
            return false;
        }

        var end = start + length;

        if (end < line.Length && line[end] != ' ')
        {
            return false;
        }

        var remainder = end < line.Length ? line[(end + 1)..] : string.Empty;

        marker = new ListMarker(isOrdered, number, columns, columns + length + 1, remainder);

        return true;
    }

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

    private readonly record struct ListMarker(
        bool IsOrdered,
        int Number,
        int MarkerColumns,
        int ContentColumns,
        string Remainder);

    private sealed record ListEntry(List<string> Content);
}
