namespace Geeratya.Jira.Adf;

/// <summary>
/// Reads Markdown-like text (<see cref="AdfMarkdownSyntax" />) into an <see cref="AdfDocument" />. Pure and
/// tolerant: any text gives a document and nothing throws. Blocks are separated by blank lines; a list, quote,
/// heading, rule or fence also starts at the next line without one. Nesting is by indentation under a list marker or
/// by <c>&gt;</c>, to at most <see cref="AdfMarkdownSyntax.MaxNesting" /> levels. A newline inside a paragraph is a
/// hard break.
/// </summary>
public static partial class AdfMarkdownParser
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

        var quoted = ConstrainContainer(ParseBlocks(inner, depth + 1), false);

        if (quoted.Count > 0)
        {
            blocks.Add(new AdfBlockquote(quoted));
        }

        return index;
    }

    private readonly record struct ListMarker(
        bool IsOrdered,
        int Number,
        int MarkerColumns,
        int ContentColumns,
        string Remainder);

    private sealed record ListEntry(List<string> Content);
}
