using System.Globalization;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Cache;

/// <summary>
/// Converts ADF documents to the cache DTOs and back. Pure. Per kind the DTO members hold: heading Number=level;
/// orderedList Number=order; taskItem Value=state; codeBlock Value=language; panel Value=panel type; table header
/// and cell Number=column span, Value=row span (absent for 1), Second=column widths joined by commas; expand and
/// nestedExpand Value=title; decisionItem Value=state; mediaInline as media; mediaSingle Value=layout; media
/// Value=id, Second=media type,
/// Third=collection, Alt, Number=width, Height; text Text and Marks; inlineCard Value=URL; emoji Value=short name,
/// Text=glyph; date Number=epoch milliseconds; status Text and Value=colour; mention Value=id, Text; unknown
/// Value=type name, Text.
/// </summary>
public static class AdfCacheMapper
{
    /// <summary>The kind of a node or mark the model does not know.</summary>
    public const string UnknownKind = "unknown";

    public static CachedAdfDocument ToDto(AdfDocument document)
    {
        return new CachedAdfDocument(document.Version, document.IsTruncated, ToNodes(document.Content));
    }

    /// <summary>The document in <paramref name="dto"/>; unreadable nodes are dropped, nothing throws.</summary>
    public static AdfDocument FromDto(CachedAdfDocument dto)
    {
        return new AdfDocument(dto.Version, FromNodes(dto.Content), dto.IsTruncated);
    }

    private static List<CachedAdfNode> ToNodes(IReadOnlyList<AdfNode> nodes)
    {
        return [.. nodes.Select(ToNode)];
    }

    private static List<CachedAdfMark> ToMarks(IReadOnlyList<AdfMark> marks)
    {
        return [.. marks.Select(ToMark)];
    }

    private static CachedAdfNode ToNode(AdfNode node)
    {
        return node switch
        {
            AdfParagraph paragraph => Container(AdfNodeTypes.Paragraph, paragraph),
            AdfHeading heading => Container(AdfNodeTypes.Heading, heading, number: heading.Level),
            AdfBulletList list => Container(AdfNodeTypes.BulletList, list),
            AdfOrderedList list => Container(AdfNodeTypes.OrderedList, list, number: list.Order),
            AdfListItem item => Container(AdfNodeTypes.ListItem, item),
            AdfTaskList list => Container(AdfNodeTypes.TaskList, list),
            AdfTaskItem item => Container(AdfNodeTypes.TaskItem, item, value: item.State.ToString()),
            AdfCodeBlock block => Container(AdfNodeTypes.CodeBlock, block, value: block.Language),
            AdfBlockquote quote => Container(AdfNodeTypes.Blockquote, quote),
            AdfPanel panel => Container(AdfNodeTypes.Panel, panel, value: panel.PanelType),
            AdfRule => Leaf(AdfNodeTypes.Rule),
            AdfTable table => Container(AdfNodeTypes.Table, table),
            AdfTableRow row => Container(AdfNodeTypes.TableRow, row),
            AdfTableHeader header => Container(AdfNodeTypes.TableHeader, header, number: header.ColumnSpan)
                with { Value = RowSpanText(header.RowSpan), Second = WidthsText(header.ColumnWidths) },
            AdfTableCell cell => Container(AdfNodeTypes.TableCell, cell, number: cell.ColumnSpan)
                with { Value = RowSpanText(cell.RowSpan), Second = WidthsText(cell.ColumnWidths) },
            AdfExpand expand => Container(AdfNodeTypes.Expand, expand, value: expand.Title),
            AdfNestedExpand expand => Container(AdfNodeTypes.NestedExpand, expand, value: expand.Title),
            AdfDecisionList list => Container(AdfNodeTypes.DecisionList, list),
            AdfDecisionItem item => Container(AdfNodeTypes.DecisionItem, item, value: item.State),
            AdfCaption caption => Container(AdfNodeTypes.Caption, caption),
            AdfMediaSingle single => Container(AdfNodeTypes.MediaSingle, single, value: single.Layout),
            AdfMediaGroup group => Container(AdfNodeTypes.MediaGroup, group),
            AdfMediaInline media => ToMedia(media) with { Kind = AdfNodeTypes.MediaInline },
            AdfMedia media => ToMedia(media),
            AdfText text => Leaf(AdfNodeTypes.Text, text: text.Text) with { Marks = ToMarks(text.Marks) },
            AdfHardBreak => Leaf(AdfNodeTypes.HardBreak),
            AdfInlineCard card => Leaf(AdfNodeTypes.InlineCard, value: card.Url),
            AdfEmoji emoji => Leaf(AdfNodeTypes.Emoji, text: emoji.Text, value: emoji.ShortName),
            AdfDate date => Leaf(AdfNodeTypes.Date, number: date.TimestampMilliseconds),
            AdfStatus status => Leaf(AdfNodeTypes.Status, text: status.Text, value: status.Color),
            AdfMention mention => Leaf(AdfNodeTypes.Mention, text: mention.Text, value: mention.Id),
            AdfUnknownNode unknown => Container(UnknownKind, unknown, text: unknown.Text, value: unknown.TypeName),
            _ => Leaf(UnknownKind, value: node.GetType().Name),
        };
    }

    private static CachedAdfNode ToMedia(AdfMedia media)
    {
        return new CachedAdfNode(
            AdfNodeTypes.Media,
            null,
            media.Id,
            media.MediaType,
            media.Collection,
            media.Alt,
            media.Width,
            media.Height,
            null,
            null);
    }

    private static CachedAdfNode Container(
        string kind,
        AdfContainerNode node,
        string? text = null,
        string? value = null,
        long? number = null)
    {
        return Leaf(kind, text, value, number) with { Children = ToNodes(node.Children) };
    }

    private static CachedAdfNode Leaf(string kind, string? text = null, string? value = null, long? number = null)
    {
        return new CachedAdfNode(kind, text, value, null, null, null, number, null, null, null);
    }

    private static CachedAdfMark ToMark(AdfMark mark)
    {
        return mark switch
        {
            AdfStrongMark => new CachedAdfMark(AdfMarkTypes.Strong, null),
            AdfEmphasisMark => new CachedAdfMark(AdfMarkTypes.Emphasis, null),
            AdfStrikeMark => new CachedAdfMark(AdfMarkTypes.Strike, null),
            AdfCodeMark => new CachedAdfMark(AdfMarkTypes.Code, null),
            AdfUnderlineMark => new CachedAdfMark(AdfMarkTypes.Underline, null),
            AdfLinkMark link => new CachedAdfMark(AdfMarkTypes.Link, link.Href),
            AdfTextColorMark color => new CachedAdfMark(AdfMarkTypes.TextColor, color.Color),
            AdfSubSupMark subSup => new CachedAdfMark(AdfMarkTypes.SubSup, subSup.Kind),
            AdfUnknownMark unknown => new CachedAdfMark(UnknownKind, unknown.TypeName),
            _ => new CachedAdfMark(UnknownKind, mark.GetType().Name),
        };
    }

    // JSON can hold nulls where the DTOs promise values, so the null checks below are not redundant.
    // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    // ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
    private static List<AdfNode> FromNodes(List<CachedAdfNode>? nodes)
    {
        return nodes is null ? [] : [.. nodes.Where(node => node?.Kind is not null).Select(FromNode)];
    }

    private static List<AdfMark> FromMarks(List<CachedAdfMark>? marks)
    {
        return marks is null ? [] : [.. marks.Where(mark => mark?.Kind is not null).Select(FromMark)];
    }

    // ReSharper restore ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
    // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract

    private static AdfNode FromNode(CachedAdfNode node)
    {
        var children = FromNodes(node.Children);

        return node.Kind switch
        {
            AdfNodeTypes.Paragraph => new AdfParagraph(children),
            AdfNodeTypes.Heading => new AdfHeading(Int(node.Number, 1), children),
            AdfNodeTypes.BulletList => new AdfBulletList(children),
            AdfNodeTypes.OrderedList => new AdfOrderedList(Int(node.Number, 1), children),
            AdfNodeTypes.ListItem => new AdfListItem(children),
            AdfNodeTypes.TaskList => new AdfTaskList(children),
            AdfNodeTypes.TaskItem => new AdfTaskItem(TaskState(node.Value), children),
            AdfNodeTypes.CodeBlock => new AdfCodeBlock(node.Value, children),
            AdfNodeTypes.Blockquote => new AdfBlockquote(children),
            AdfNodeTypes.Panel => new AdfPanel(node.Value ?? string.Empty, children),
            AdfNodeTypes.Rule => new AdfRule(),
            AdfNodeTypes.Table => new AdfTable(children),
            AdfNodeTypes.TableRow => new AdfTableRow(children),
            AdfNodeTypes.TableHeader => new AdfTableHeader(Int(node.Number, 1), children)
            {
                RowSpan = RowSpan(node.Value),
                ColumnWidths = Widths(node.Second),
            },
            AdfNodeTypes.TableCell => new AdfTableCell(Int(node.Number, 1), children)
            {
                RowSpan = RowSpan(node.Value),
                ColumnWidths = Widths(node.Second),
            },
            AdfNodeTypes.Expand => new AdfExpand(node.Value, children),
            AdfNodeTypes.NestedExpand => new AdfNestedExpand(node.Value, children),
            AdfNodeTypes.DecisionList => new AdfDecisionList(children),
            AdfNodeTypes.DecisionItem => new AdfDecisionItem(node.Value, children),
            AdfNodeTypes.Caption => new AdfCaption(children),
            AdfNodeTypes.MediaSingle => new AdfMediaSingle(node.Value, children),
            AdfNodeTypes.MediaGroup => new AdfMediaGroup(children),
            AdfNodeTypes.Media => FromMedia(node),
            AdfNodeTypes.MediaInline => FromMediaInline(node),
            AdfNodeTypes.Text => new AdfText(node.Text ?? string.Empty, FromMarks(node.Marks)),
            AdfNodeTypes.HardBreak => new AdfHardBreak(),
            AdfNodeTypes.InlineCard => new AdfInlineCard(node.Value),
            AdfNodeTypes.Emoji => new AdfEmoji(node.Value, node.Text),
            AdfNodeTypes.Date => new AdfDate(node.Number),
            AdfNodeTypes.Status => new AdfStatus(node.Text ?? string.Empty, node.Value),
            AdfNodeTypes.Mention => new AdfMention(node.Value, node.Text),
            _ => new AdfUnknownNode(node.Value ?? node.Kind, node.Text, children),
        };
    }

    // A row span and column widths ride in the string slots of the flat shape; absent means 1 and none, so files
    // written before these attributes existed read back unchanged.
    private static string? RowSpanText(int rowSpan)
    {
        return rowSpan > 1 ? rowSpan.ToString(CultureInfo.InvariantCulture) : null;
    }

    private static int RowSpan(string? text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var span) && span > 1 ? span : 1;
    }

    private static string? WidthsText(IReadOnlyList<int> widths)
    {
        var texts = widths.Select(width => width.ToString(CultureInfo.InvariantCulture));

        return widths.Count == 0 ? null : string.Join(',', texts);
    }

    private static List<int> Widths(string? text)
    {
        var widths = new List<int>();

        foreach (var part in (text ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var width))
            {
                return [];
            }

            widths.Add(width);
        }

        return widths;
    }

    private static AdfMedia FromMedia(CachedAdfNode node)
    {
        int? width = node.Number is null ? null : Int(node.Number, 0);

        return new AdfMedia(node.Value, node.Second, node.Third, node.Alt, width, node.Height);
    }

    private static AdfMediaInline FromMediaInline(CachedAdfNode node)
    {
        var media = FromMedia(node);

        return new AdfMediaInline(media.Id, media.MediaType, media.Collection, media.Alt, media.Width, media.Height);
    }

    private static AdfMark FromMark(CachedAdfMark mark)
    {
        return mark.Kind switch
        {
            AdfMarkTypes.Strong => new AdfStrongMark(),
            AdfMarkTypes.Emphasis => new AdfEmphasisMark(),
            AdfMarkTypes.Strike => new AdfStrikeMark(),
            AdfMarkTypes.Code => new AdfCodeMark(),
            AdfMarkTypes.Underline => new AdfUnderlineMark(),
            AdfMarkTypes.Link => new AdfLinkMark(mark.Value),
            AdfMarkTypes.TextColor => new AdfTextColorMark(mark.Value),
            AdfMarkTypes.SubSup => new AdfSubSupMark(mark.Value),
            _ => new AdfUnknownMark(mark.Value ?? mark.Kind),
        };
    }

    private static int Int(long? number, int fallback)
    {
        return number is >= int.MinValue and <= int.MaxValue ? (int)number : fallback;
    }

    private static AdfTaskState TaskState(string? name)
    {
        return Enum.TryParse(name, out AdfTaskState state) && Enum.IsDefined(state) ? state : AdfTaskState.Todo;
    }
}
