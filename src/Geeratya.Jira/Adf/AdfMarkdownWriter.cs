using System.Globalization;
using System.Text;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Writes an <see cref="AdfDocument" /> as Markdown-like text for editing (<see cref="AdfMarkdownSyntax" />). Pure.
/// Paragraphs, headings, bold, italic, strike, underline, code, links, bullet, ordered and task lists, code blocks,
/// quotes, rules, mentions and inline cards survive; a node the text cannot express (table, panel, expand, media,
/// decision, unknown) is flattened to its text, so <see cref="AdfRoundTrip" /> reports the document.
/// </summary>
public static class AdfMarkdownWriter
{
    private const string BlockSeparator = "\n\n";
    private const char LineSeparator = '\n';

    public static string ToText(AdfDocument document)
    {
        return WriteBlocks(document.Content, false);
    }

    private static string WriteBlocks(IReadOnlyList<AdfNode> blocks, bool isListItem)
    {
        var builder = new StringBuilder();
        AdfNode? previous = null;

        for (var index = 0; index < blocks.Count; index++)
        {
            var node = blocks[index];
            string text;

            if (IsInline(node))
            {
                var run = new List<AdfNode>();

                while (index < blocks.Count && IsInline(blocks[index]))
                {
                    run.Add(blocks[index]);
                    index++;
                }

                index--;
                node = new AdfParagraph(run);
                text = AdfMarkdownInlineWriter.Write(run);
            }
            else
            {
                text = WriteBlock(node);
            }

            if (text.Length == 0)
            {
                continue;
            }

            if (previous is not null)
            {
                builder.Append(IsTight(previous, node, isListItem) ? "\n" : BlockSeparator);
            }

            builder.Append(text);
            previous = node;
        }

        return builder.ToString();
    }

    private static string WriteBlock(AdfNode node)
    {
        switch (node)
        {
            case AdfParagraph paragraph:
                return AdfMarkdownInlineWriter.Write(paragraph.Children);
            case AdfHeading heading:
                return WriteHeading(heading);
            case AdfBulletList bulletList:
                return WriteList(bulletList.Children, 0, false);
            case AdfOrderedList orderedList:
                return WriteList(orderedList.Children, orderedList.Order, true);
            case AdfTaskList taskList:
                return WriteTaskList(taskList);
            case AdfCodeBlock codeBlock:
                return WriteCodeBlock(codeBlock);
            case AdfBlockquote blockquote:
                return WriteQuote(blockquote);
            case AdfRule:
                return AdfMarkdownSyntax.Rule;
            case AdfUnknownNode { Children.Count: 0, Text: { } text }:
                return AdfMarkdownInlineWriter.Write([new AdfText(text, [])]);
            case AdfContainerNode container:
                return WriteBlocks(container.Children, false);
            default:
                return string.Empty;
        }
    }

    private static string WriteHeading(AdfHeading heading)
    {
        var level = Math.Clamp(heading.Level, 1, AdfMarkdownSyntax.MaxHeadingLevel);
        var content = AdfMarkdownInlineWriter.Write(heading.Children);

        return $"{new string(AdfMarkdownSyntax.HeadingMarker, level)} {content}";
    }

    private static string WriteList(IReadOnlyList<AdfNode> items, int firstNumber, bool isOrdered)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < items.Count; index++)
        {
            var children = items[index] is AdfListItem item ? item.Children : [items[index]];
            var marker = isOrdered
                ? $"{(firstNumber + index).ToString(CultureInfo.InvariantCulture)}. "
                : AdfMarkdownSyntax.BulletMarker;

            if (index > 0)
            {
                builder.Append(LineSeparator);
            }

            AppendPrefixed(builder, WriteBlocks(children, true), marker, new string(' ', marker.Length), false);
        }

        return builder.ToString();
    }

    private static string WriteTaskList(AdfTaskList taskList)
    {
        var builder = new StringBuilder();

        for (var index = 0; index < taskList.Children.Count; index++)
        {
            var isDone = taskList.Children[index] is AdfTaskItem { State: AdfTaskState.Done };
            var marker = AdfMarkdownSyntax.BulletMarker
                + (isDone ? AdfMarkdownSyntax.TaskDoneMarker : AdfMarkdownSyntax.TaskTodoMarker);
            var children = taskList.Children[index] is AdfContainerNode container
                ? container.Children
                : [taskList.Children[index]];
            var content = AdfMarkdownInlineWriter.Write(children);

            if (index > 0)
            {
                builder.Append(LineSeparator);
            }

            AppendPrefixed(builder, content, marker, new string(' ', AdfMarkdownSyntax.BulletMarker.Length), false);
        }

        return builder.ToString();
    }

    private static string WriteCodeBlock(AdfCodeBlock codeBlock)
    {
        var code = new StringBuilder();

        for (var index = 0; index < codeBlock.Children.Count; index++)
        {
            if (codeBlock.Children[index] is AdfText text)
            {
                code.Append(text.Text);
            }
        }

        var fence = new string(AdfMarkdownSyntax.CodeFence, FenceLength(code.ToString()));
        var builder = new StringBuilder();

        builder.Append(fence).Append(codeBlock.Language).Append(LineSeparator);
        builder.Append(code).Append(LineSeparator);
        builder.Append(fence);

        return builder.ToString();
    }

    private static string WriteQuote(AdfBlockquote blockquote)
    {
        var builder = new StringBuilder();

        AppendPrefixed(
            builder,
            WriteBlocks(blockquote.Children, false),
            AdfMarkdownSyntax.QuotePrefix,
            AdfMarkdownSyntax.QuotePrefix,
            true);

        return builder.ToString();
    }

    /// <summary>Fence one longer than the longest backtick run at the start of a line of the code.</summary>
    private static int FenceLength(string code)
    {
        var longest = AdfMarkdownSyntax.MinFenceLength - 1;
        var run = 0;
        var atLineStart = true;

        for (var index = 0; index < code.Length; index++)
        {
            if (code[index] == AdfMarkdownSyntax.CodeFence && atLineStart)
            {
                run++;
                longest = Math.Max(longest, run);

                continue;
            }

            run = 0;
            atLineStart = code[index] == '\n';
        }

        return longest + 1;
    }

    private static void AppendPrefixed(
        StringBuilder builder,
        string text,
        string firstPrefix,
        string restPrefix,
        bool keepBlankPrefix)
    {
        var lineStart = 0;
        var isFirst = true;

        while (lineStart <= text.Length)
        {
            var lineEnd = text.IndexOf('\n', lineStart);

            if (lineEnd < 0)
            {
                lineEnd = text.Length;
            }

            var prefix = isFirst ? firstPrefix : restPrefix;
            var isBlank = lineEnd == lineStart;

            if (!isBlank || keepBlankPrefix || isFirst)
            {
                builder.Append(isBlank ? prefix.TrimEnd() : prefix);
            }

            builder.Append(text, lineStart, lineEnd - lineStart);

            if (lineEnd < text.Length)
            {
                builder.Append('\n');
            }

            isFirst = false;
            lineStart = lineEnd + 1;
        }
    }

    private static bool IsTight(AdfNode previous, AdfNode next, bool isListItem)
    {
        return isListItem && previous is AdfParagraph && next is AdfBulletList or AdfOrderedList or AdfTaskList;
    }

    private static bool IsInline(AdfNode node)
    {
        return node is AdfText or AdfHardBreak or AdfInlineCard or AdfEmoji or AdfDate or AdfStatus or AdfMention
            or AdfMediaInline;
    }
}
