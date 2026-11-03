using System.Globalization;

namespace Geeratya.Jira.Adf;

public static partial class AdfMarkdownParser
{
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

        var list = BuildList(entries, depth, isOrdered, firstNumber);

        if (list is AdfContainerNode { Children.Count: > 0 })
        {
            blocks.Add(list);
        }

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
            var content = ConstrainContainer(ParseBlocks(entries[index].Content, depth + 1), true);

            if (content.Count > 0)
            {
                items.Add(new AdfListItem(content));
            }
        }

        return isOrdered ? new AdfOrderedList(firstNumber, items) : new AdfBulletList(items);
    }

    /// <summary>
    /// Keeps the blocks Jira accepts inside a quote or list item: a heading becomes a paragraph, a rule is dropped, a
    /// nested quote is flattened into its blocks, and a list item starts with a paragraph (an empty one if needed).
    /// Without this the text <c>&gt; # note</c> would give ADF that Jira rejects with 400.
    /// </summary>
    private static List<AdfNode> ConstrainContainer(List<AdfNode> blocks, bool isListItem)
    {
        var allowed = new List<AdfNode>(blocks.Count);

        for (var index = 0; index < blocks.Count; index++)
        {
            switch (blocks[index])
            {
                case AdfHeading heading:
                    allowed.Add(new AdfParagraph(heading.Children));

                    break;
                case AdfRule:
                    break;
                case AdfBlockquote quote:
                    allowed.AddRange(quote.Children);

                    break;
                default:
                    allowed.Add(blocks[index]);

                    break;
            }
        }

        if (isListItem && allowed.Count > 0 && allowed[0] is not AdfParagraph)
        {
            allowed.Insert(0, new AdfParagraph([]));
        }

        return allowed;
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
}
