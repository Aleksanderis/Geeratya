using System.Globalization;
using System.Text;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Adf;

/// <summary>
/// Block layout: paragraphs, headings, lists, quotes, panels, code, rules, expands and media markers. Containers
/// render their children into a narrower context and then put a prefix in front of every line. Siblings are
/// separated by one blank line, except inside list items and table cells (tight).
/// </summary>
internal static class AdfBlockRenderer
{
    /// <summary>Nesting deeper than this is cut off; the parser stops at 64, so only hand-built trees reach it.</summary>
    public const int MaxDepth = 100;

    public static void RenderBlocks(
        IReadOnlyList<AdfNode> nodes,
        RenderContext context,
        bool tight,
        List<StyledLine> output)
    {
        if (context.Depth > MaxDepth)
        {
            output.Add(StyledLine.Of(AdfMarkers.Truncated, TextRole.Muted));

            return;
        }

        var produced = false;
        var index = 0;

        while (index < nodes.Count)
        {
            var separator = produced && !tight;

            if (separator)
            {
                output.Add(StyledLine.Empty);
            }

            var before = output.Count;
            var node = nodes[index];

            if (IsInline(node))
            {
                var end = index;

                while (end < nodes.Count && IsInline(nodes[end]))
                {
                    end++;
                }

                RenderInline(nodes, index, end, context, context.BaseRole, output);
                index = end;
            }
            else
            {
                RenderBlock(node, context, output);
                index++;
            }

            if (output.Count > before)
            {
                produced = true;
            }
            else if (separator)
            {
                output.RemoveAt(before - 1);
            }
        }
    }

    /// <summary>Renders children in a narrower context and puts the prefixes in front of the lines.</summary>
    public static void RenderPrefixed(
        IReadOnlyList<AdfNode> children,
        RenderContext context,
        bool tight,
        LinePrefix first,
        LinePrefix rest,
        List<StyledLine> output)
    {
        var inner = new List<StyledLine>();
        var prefixWidth = Math.Max(first.Width, rest.Width);
        var usable = context.Width - prefixWidth >= 1;

        RenderBlocks(children, usable ? context.Narrow(prefixWidth) : context.Deeper(), tight, inner);
        AppendPrefixed(inner, usable ? first : LinePrefix.None, usable ? rest : LinePrefix.None, output);
    }

    public static void AppendPrefixed(
        List<StyledLine> inner,
        LinePrefix first,
        LinePrefix rest,
        List<StyledLine> output)
    {
        for (var index = 0; index < inner.Count; index++)
        {
            var prefix = index == 0 ? first : rest;
            var line = inner[index];

            if (prefix.Text.Length == 0)
            {
                output.Add(line);
            }
            else if (line.Spans.Count == 0)
            {
                output.Add(PrefixOnly(prefix));
            }
            else
            {
                var spans = new List<StyledSpan>(line.Spans.Count + 1) { new(prefix.Text, prefix.Role) };

                spans.AddRange(line.Spans);
                output.Add(new StyledLine(spans));
            }
        }
    }

    public static void RenderInline(
        IReadOnlyList<AdfNode> nodes,
        int start,
        int end,
        RenderContext context,
        TextRole role,
        List<StyledLine> output)
    {
        var writer = new AdfInlineWriter(context.Width, role);

        for (var index = start; index < end; index++)
        {
            writer.Write(nodes[index]);
        }

        writer.Finish(output);
    }

    private static StyledLine PrefixOnly(LinePrefix prefix)
    {
        var trimmed = prefix.Text.TrimEnd();

        return trimmed.Length == 0 ? StyledLine.Empty : StyledLine.Of(trimmed, prefix.Role);
    }

    private static bool IsInline(AdfNode node)
    {
        return node is AdfText or AdfHardBreak or AdfInlineCard or AdfEmoji or AdfDate or AdfStatus or AdfMention;
    }

    private static void RenderBlock(AdfNode node, RenderContext context, List<StyledLine> output)
    {
        switch (node)
        {
            case AdfParagraph paragraph:
                RenderInline(paragraph.Children, 0, paragraph.Children.Count, context, context.BaseRole, output);
                break;
            case AdfHeading heading:
                RenderInline(heading.Children, 0, heading.Children.Count, context, TextRole.Heading, output);
                break;
            case AdfBulletList bullets:
                RenderBulletList(bullets, context, output);
                break;
            case AdfOrderedList ordered:
                RenderOrderedList(ordered, context, output);
                break;
            case AdfTaskList tasks:
                RenderTaskList(tasks, context, output);
                break;
            case AdfCodeBlock code:
                RenderCodeBlock(code, context, output);
                break;
            case AdfBlockquote quote:
                RenderQuote(quote, context, output);
                break;
            case AdfPanel panel:
                RenderPanel(panel, context, output);
                break;
            case AdfRule:
                RenderRule(context, output);
                break;
            case AdfExpand expand:
                RenderExpand(expand, context, output);
                break;
            case AdfTable table:
                AdfTableRenderer.Render(table, context, output);
                break;
            case AdfMediaSingle single:
                RenderMedia(single.Children, AdfMarkers.Image, context, output);
                break;
            case AdfMediaGroup group:
                RenderMedia(group.Children, AdfMarkers.File, context, output);
                break;
            case AdfMedia media:
                RenderMedia([media], AdfMarkers.File, context, output);
                break;
            case AdfUnknownNode unknown:
                RenderUnknown(unknown, context, output);
                break;
            case AdfContainerNode container:
                RenderBlocks(container.Children, context.Deeper(), false, output);
                break;
        }
    }

    private static void RenderBulletList(AdfBulletList list, RenderContext context, List<StyledLine> output)
    {
        var marker = new LinePrefix(AdfMarkers.BulletMarker, TextRole.Muted);

        for (var index = 0; index < list.Children.Count; index++)
        {
            RenderItem(list.Children[index], context, marker, output);
        }
    }

    private static void RenderOrderedList(AdfOrderedList list, RenderContext context, List<StyledLine> output)
    {
        var lastNumber = list.Order + list.Children.Count - 1;
        var numberWidth = lastNumber.ToString(CultureInfo.InvariantCulture).Length;

        for (var index = 0; index < list.Children.Count; index++)
        {
            var number = (list.Order + index).ToString(CultureInfo.InvariantCulture);
            var marker = new LinePrefix($"{number.PadLeft(numberWidth)}. ", TextRole.Muted);

            RenderItem(list.Children[index], context, marker, output);
        }
    }

    private static void RenderTaskList(AdfTaskList list, RenderContext context, List<StyledLine> output)
    {
        for (var index = 0; index < list.Children.Count; index++)
        {
            var item = list.Children[index];
            var done = item is AdfTaskItem { State: AdfTaskState.Done };
            var marker = new LinePrefix(done ? AdfMarkers.TaskDone : AdfMarkers.TaskTodo, TextRole.Muted);

            RenderItem(item, context, marker, output);
        }
    }

    private static void RenderItem(AdfNode item, RenderContext context, LinePrefix marker, List<StyledLine> output)
    {
        var children = item is AdfContainerNode container ? container.Children : [item];
        var inner = new List<StyledLine>();
        var usable = context.Width - marker.Width >= 1;

        RenderBlocks(children, usable ? context.Narrow(marker.Width) : context.Deeper(), true, inner);

        if (inner.Count == 0)
        {
            inner.Add(StyledLine.Empty);
        }

        AppendPrefixed(inner, usable ? marker : LinePrefix.None, usable ? marker.Hanging() : LinePrefix.None, output);
    }

    private static void RenderCodeBlock(AdfCodeBlock code, RenderContext context, List<StyledLine> output)
    {
        var fence = new WrappedText(context.Width, true);
        var opening = string.IsNullOrWhiteSpace(code.Language)
            ? AdfMarkers.Fence
            : $"{AdfMarkers.Fence}{code.Language}";

        fence.Append(opening, TextRole.Muted);
        fence.Finish(output);

        var body = new WrappedText(context.Width, true);

        body.Append(PlainText(code.Children), TextRole.Code);
        body.Finish(output);

        var closing = new WrappedText(context.Width, true);

        closing.Append(AdfMarkers.Fence, TextRole.Muted);
        closing.Finish(output);
    }

    private static void RenderQuote(AdfBlockquote quote, RenderContext context, List<StyledLine> output)
    {
        var bar = new LinePrefix(AdfMarkers.QuoteBar, TextRole.Muted);

        RenderPrefixed(quote.Children, context.WithBase(TextRole.Quote), false, bar, bar, output);
    }

    private static void RenderPanel(AdfPanel panel, RenderContext context, List<StyledLine> output)
    {
        var bar = new LinePrefix(AdfMarkers.QuoteBar, TextRole.Muted);
        var inner = new List<StyledLine>();
        var usable = context.Width - bar.Width >= 1;
        var innerContext = usable ? context.Narrow(bar.Width) : context.Deeper();

        inner.Add(StyledLine.Of($"[{panel.PanelType}]", TextRole.Strong));
        RenderBlocks(panel.Children, innerContext, false, inner);
        AppendPrefixed(inner, usable ? bar : LinePrefix.None, usable ? bar : LinePrefix.None, output);
    }

    private static void RenderRule(RenderContext context, List<StyledLine> output)
    {
        var length = Math.Min(context.Width, AdfMarkers.MaxRuleLength);

        output.Add(StyledLine.Of(new string(AdfMarkers.RuleCharacter, length), TextRole.Muted));
    }

    private static void RenderExpand(AdfExpand expand, RenderContext context, List<StyledLine> output)
    {
        var title = string.IsNullOrWhiteSpace(expand.Title) ? AdfMarkers.DefaultExpandTitle : expand.Title;
        var heading = new WrappedText(context.Width, false);

        heading.Append($"{AdfMarkers.ExpandOpen}{title}", TextRole.Strong);
        heading.Finish(output);

        var indent = new LinePrefix(AdfMarkers.ExpandIndent, TextRole.Body);

        RenderPrefixed(expand.Children, context, false, indent, indent, output);
    }

    private static void RenderMedia(
        IReadOnlyList<AdfNode> children,
        string marker,
        RenderContext context,
        List<StyledLine> output)
    {
        var writer = new AdfInlineWriter(context.Width, TextRole.Muted);
        var written = false;

        for (var index = 0; index < children.Count; index++)
        {
            if (written)
            {
                writer.LineBreak();
            }

            written = true;

            if (children[index] is AdfMedia media)
            {
                writer.WriteMarker(AdfInlineWriter.MediaMarker(media, marker));
            }
            else
            {
                writer.Write(children[index]);
            }
        }

        if (!written)
        {
            writer.WriteMarker(marker);
        }

        writer.Finish(output);
    }

    private static void RenderUnknown(AdfUnknownNode unknown, RenderContext context, List<StyledLine> output)
    {
        var writer = new AdfInlineWriter(context.Width, context.BaseRole);

        writer.Write(unknown with { Children = [] });
        writer.Finish(output);
        RenderBlocks(unknown.Children, context.Deeper(), false, output);
    }

    private static string PlainText(IReadOnlyList<AdfNode> nodes)
    {
        var builder = new StringBuilder();

        AppendPlainText(nodes, builder, 0);

        return builder.ToString();
    }

    private static void AppendPlainText(IReadOnlyList<AdfNode> nodes, StringBuilder builder, int depth)
    {
        if (depth > MaxDepth)
        {
            return;
        }

        for (var index = 0; index < nodes.Count; index++)
        {
            switch (nodes[index])
            {
                case AdfText text:
                    builder.Append(text.Text);
                    break;
                case AdfHardBreak:
                    builder.Append('\n');
                    break;
                case AdfUnknownNode unknown:
                    builder.Append(unknown.Text);
                    AppendPlainText(unknown.Children, builder, depth + 1);
                    break;
                case AdfContainerNode container:
                    AppendPlainText(container.Children, builder, depth + 1);
                    break;
            }
        }
    }
}
