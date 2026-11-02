using System.Buffers;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Writes an <see cref="AdfDocument" /> as ADF JSON, the body of a description or comment write. Pure and
/// deterministic: adjacent text with the same marks is joined, marks are written in a fixed order, empty text is
/// skipped and the <c>localId</c> that task lists need are numbered in document order, so equal documents give equal
/// JSON (the round-trip check compares that). Every node of the model can be written, but media ids cannot be
/// invented: Jira rejects media it does not know (<c>docs/jira-layer.md</c>).
/// </summary>
public static class AdfJsonWriter
{
    private const string LocalIdProperty = "localId";
    private const string TaskListIdPrefix = "tl";
    private const string TaskItemIdPrefix = "t";
    private const string DecisionListIdPrefix = "dl";
    private const string DecisionItemIdPrefix = "d";

    private static readonly JsonWriterOptions _writerOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ToJson(AdfDocument document)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer, _writerOptions))
        {
            Write(writer, document);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    public static void Write(Utf8JsonWriter writer, AdfDocument document)
    {
        var state = new WriteState(writer);

        writer.WriteStartObject();
        writer.WriteNumber(AdfPropertyNames.Version, document.Version);
        writer.WriteString(AdfPropertyNames.Type, AdfNodeTypes.Doc);
        WriteContent(state, document.Content);
        writer.WriteEndObject();
    }

    private static void WriteContent(WriteState state, IReadOnlyList<AdfNode> children)
    {
        var nodes = AdfInlineRuns.Merge(children);

        if (nodes.Count == 0)
        {
            return;
        }

        state.Writer.WriteStartArray(AdfPropertyNames.Content);

        for (var index = 0; index < nodes.Count; index++)
        {
            WriteNode(state, nodes[index]);
        }

        state.Writer.WriteEndArray();
    }

    private static void WriteNode(WriteState state, AdfNode node)
    {
        switch (node)
        {
            case AdfText text:
                WriteText(state.Writer, text);

                break;
            case AdfParagraph paragraph:
                WritePlain(state, AdfNodeTypes.Paragraph, paragraph.Children);

                break;
            case AdfBulletList bulletList:
                WritePlain(state, AdfNodeTypes.BulletList, bulletList.Children);

                break;
            case AdfListItem listItem:
                WritePlain(state, AdfNodeTypes.ListItem, listItem.Children);

                break;
            case AdfBlockquote blockquote:
                WritePlain(state, AdfNodeTypes.Blockquote, blockquote.Children);

                break;
            case AdfTable table:
                WritePlain(state, AdfNodeTypes.Table, table.Children);

                break;
            case AdfTableRow tableRow:
                WritePlain(state, AdfNodeTypes.TableRow, tableRow.Children);

                break;
            case AdfCaption caption:
                WritePlain(state, AdfNodeTypes.Caption, caption.Children);

                break;
            case AdfMediaGroup mediaGroup:
                WritePlain(state, AdfNodeTypes.MediaGroup, mediaGroup.Children);

                break;
            case AdfHardBreak:
                WriteEmpty(state.Writer, AdfNodeTypes.HardBreak);

                break;
            case AdfRule:
                WriteEmpty(state.Writer, AdfNodeTypes.Rule);

                break;
            default:
                WriteWithAttributes(state, node);

                break;
        }
    }

    private static void WriteWithAttributes(WriteState state, AdfNode node)
    {
        var writer = state.Writer;

        switch (node)
        {
            case AdfHeading heading:
                WriteStart(writer, AdfNodeTypes.Heading);
                WriteAttributes(writer, AdfAttributeNames.Level, heading.Level);
                WriteContent(state, heading.Children);

                break;
            case AdfOrderedList orderedList:
                WriteStart(writer, AdfNodeTypes.OrderedList);

                if (orderedList.Order != 1)
                {
                    WriteAttributes(writer, AdfAttributeNames.Order, orderedList.Order);
                }

                WriteContent(state, orderedList.Children);

                break;
            case AdfTaskList taskList:
                WriteStart(writer, AdfNodeTypes.TaskList);
                WriteLocalId(state, TaskListIdPrefix);
                WriteContent(state, taskList.Children);

                break;
            case AdfTaskItem taskItem:
                WriteStart(writer, AdfNodeTypes.TaskItem);
                WriteTaskItemAttributes(state, taskItem);
                WriteContent(state, taskItem.Children);

                break;
            case AdfCodeBlock codeBlock:
                WriteStart(writer, AdfNodeTypes.CodeBlock);

                if (codeBlock.Language is not null)
                {
                    WriteAttributes(writer, AdfAttributeNames.Language, codeBlock.Language);
                }

                WriteContent(state, codeBlock.Children);

                break;
            case AdfPanel panel:
                WriteStart(writer, AdfNodeTypes.Panel);
                WriteAttributes(writer, AdfAttributeNames.PanelType, panel.PanelType);
                WriteContent(state, panel.Children);

                break;
            case AdfTableHeader header:
                WriteStart(writer, AdfNodeTypes.TableHeader);
                WriteCellAttributes(writer, header.ColumnSpan, header.RowSpan, header.ColumnWidths);
                WriteContent(state, header.Children);

                break;
            case AdfTableCell cell:
                WriteStart(writer, AdfNodeTypes.TableCell);
                WriteCellAttributes(writer, cell.ColumnSpan, cell.RowSpan, cell.ColumnWidths);
                WriteContent(state, cell.Children);

                break;
            case AdfExpand expand:
                WriteStart(writer, AdfNodeTypes.Expand);
                WriteOptionalAttribute(writer, AdfAttributeNames.Title, expand.Title);
                WriteContent(state, expand.Children);

                break;
            case AdfNestedExpand nestedExpand:
                WriteStart(writer, AdfNodeTypes.NestedExpand);
                WriteOptionalAttribute(writer, AdfAttributeNames.Title, nestedExpand.Title);
                WriteContent(state, nestedExpand.Children);

                break;
            case AdfDecisionList decisionList:
                WriteStart(writer, AdfNodeTypes.DecisionList);
                WriteLocalId(state, DecisionListIdPrefix);
                WriteContent(state, decisionList.Children);

                break;
            case AdfDecisionItem decisionItem:
                WriteStart(writer, AdfNodeTypes.DecisionItem);
                WriteDecisionItemAttributes(state, decisionItem);
                WriteContent(state, decisionItem.Children);

                break;
            case AdfMediaSingle mediaSingle:
                WriteStart(writer, AdfNodeTypes.MediaSingle);
                WriteOptionalAttribute(writer, AdfAttributeNames.Layout, mediaSingle.Layout);
                WriteContent(state, mediaSingle.Children);

                break;
            case AdfUnknownNode unknown:
                WriteStart(writer, unknown.TypeName);

                if (unknown.Text is not null)
                {
                    writer.WriteString(AdfPropertyNames.Text, unknown.Text);
                }

                WriteContent(state, unknown.Children);

                break;
            default:
                WriteLeaf(writer, node);

                return;
        }

        writer.WriteEndObject();
    }

    private static void WriteLeaf(Utf8JsonWriter writer, AdfNode node)
    {
        switch (node)
        {
            case AdfInlineCard card:
                WriteStart(writer, AdfNodeTypes.InlineCard);
                WriteOptionalAttribute(writer, AdfAttributeNames.Url, card.Url);

                break;
            case AdfEmoji emoji:
                WriteStart(writer, AdfNodeTypes.Emoji);
                WriteTwoAttributes(
                    writer,
                    AdfAttributeNames.ShortName,
                    emoji.ShortName,
                    AdfPropertyNames.Text,
                    emoji.Text);

                break;
            case AdfDate date:
                WriteStart(writer, AdfNodeTypes.Date);

                if (date.TimestampMilliseconds is { } milliseconds)
                {
                    WriteAttributes(
                        writer,
                        AdfAttributeNames.Timestamp,
                        milliseconds.ToString(CultureInfo.InvariantCulture));
                }

                break;
            case AdfStatus status:
                WriteStart(writer, AdfNodeTypes.Status);
                WriteTwoAttributes(writer, AdfPropertyNames.Text, status.Text, AdfAttributeNames.Color, status.Color);

                break;
            case AdfMention mention:
                WriteStart(writer, AdfNodeTypes.Mention);
                WriteTwoAttributes(writer, AdfAttributeNames.Id, mention.Id, AdfPropertyNames.Text, mention.Text);

                break;
            case AdfMedia media:
                WriteStart(writer, media is AdfMediaInline ? AdfNodeTypes.MediaInline : AdfNodeTypes.Media);
                WriteMediaAttributes(writer, media);

                break;
            default:
                WriteStart(writer, AdfNodeTypes.Paragraph);

                break;
        }

        writer.WriteEndObject();
    }

    private static void WritePlain(WriteState state, string type, IReadOnlyList<AdfNode> children)
    {
        WriteStart(state.Writer, type);
        WriteContent(state, children);
        state.Writer.WriteEndObject();
    }

    private static void WriteEmpty(Utf8JsonWriter writer, string type)
    {
        WriteStart(writer, type);
        writer.WriteEndObject();
    }

    private static void WriteStart(Utf8JsonWriter writer, string type)
    {
        writer.WriteStartObject();
        writer.WriteString(AdfPropertyNames.Type, type);
    }

    private static void WriteAttributes(Utf8JsonWriter writer, string name, int value)
    {
        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteNumber(name, value);
        writer.WriteEndObject();
    }

    private static void WriteAttributes(Utf8JsonWriter writer, string name, string value)
    {
        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteString(name, value);
        writer.WriteEndObject();
    }

    private static void WriteOptionalAttribute(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            WriteAttributes(writer, name, value);
        }
    }

    private static void WriteTwoAttributes(
        Utf8JsonWriter writer,
        string firstName,
        string? firstValue,
        string secondName,
        string? secondValue)
    {
        if (firstValue is null && secondValue is null)
        {
            return;
        }

        writer.WriteStartObject(AdfPropertyNames.Attributes);
        WriteIfPresent(writer, firstName, firstValue);
        WriteIfPresent(writer, secondName, secondValue);
        writer.WriteEndObject();
    }

    private static void WriteIfPresent(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(name, value);
        }
    }

    private static void WriteLocalId(WriteState state, string prefix)
    {
        state.LocalIdCount++;

        WriteAttributes(
            state.Writer,
            LocalIdProperty,
            $"{prefix}{state.LocalIdCount.ToString(CultureInfo.InvariantCulture)}");
    }

    private static void WriteTaskItemAttributes(WriteState state, AdfTaskItem item)
    {
        state.LocalIdCount++;

        var writer = state.Writer;

        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteString(
            LocalIdProperty,
            $"{TaskItemIdPrefix}{state.LocalIdCount.ToString(CultureInfo.InvariantCulture)}");
        writer.WriteString(
            AdfAttributeNames.State,
            item.State == AdfTaskState.Done ? AdfTaskStateNames.Done : AdfTaskStateNames.Todo);
        writer.WriteEndObject();
    }

    private static void WriteDecisionItemAttributes(WriteState state, AdfDecisionItem item)
    {
        state.LocalIdCount++;

        var writer = state.Writer;

        writer.WriteStartObject(AdfPropertyNames.Attributes);
        writer.WriteString(
            LocalIdProperty,
            $"{DecisionItemIdPrefix}{state.LocalIdCount.ToString(CultureInfo.InvariantCulture)}");
        writer.WriteString(AdfAttributeNames.State, item.State ?? string.Empty);
        writer.WriteEndObject();
    }

    private static void WriteCellAttributes(
        Utf8JsonWriter writer,
        int columnSpan,
        int rowSpan,
        IReadOnlyList<int> columnWidths)
    {
        if (columnSpan == 1 && rowSpan == 1 && columnWidths.Count == 0)
        {
            return;
        }

        writer.WriteStartObject(AdfPropertyNames.Attributes);

        if (columnSpan != 1)
        {
            writer.WriteNumber(AdfAttributeNames.ColumnSpan, columnSpan);
        }

        if (rowSpan != 1)
        {
            writer.WriteNumber(AdfAttributeNames.RowSpan, rowSpan);
        }

        if (columnWidths.Count > 0)
        {
            writer.WriteStartArray(AdfAttributeNames.ColumnWidth);

            for (var index = 0; index < columnWidths.Count; index++)
            {
                writer.WriteNumberValue(columnWidths[index]);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteMediaAttributes(Utf8JsonWriter writer, AdfMedia media)
    {
        writer.WriteStartObject(AdfPropertyNames.Attributes);
        WriteIfPresent(writer, AdfAttributeNames.Id, media.Id);
        WriteIfPresent(writer, AdfPropertyNames.Type, media.MediaType);
        WriteIfPresent(writer, AdfAttributeNames.Collection, media.Collection);
        WriteIfPresent(writer, AdfAttributeNames.Alt, media.Alt);

        if (media.Width is { } width)
        {
            writer.WriteNumber(AdfAttributeNames.Width, width);
        }

        if (media.Height is { } height)
        {
            writer.WriteNumber(AdfAttributeNames.Height, height);
        }

        writer.WriteEndObject();
    }

    private static void WriteText(Utf8JsonWriter writer, AdfText text)
    {
        WriteStart(writer, AdfNodeTypes.Text);
        writer.WriteString(AdfPropertyNames.Text, text.Text);

        if (text.Marks.Count > 0)
        {
            writer.WriteStartArray(AdfPropertyNames.Marks);

            for (var rank = 0; rank < AdfInlineRuns.RankCount; rank++)
            {
                for (var index = 0; index < text.Marks.Count; index++)
                {
                    if (AdfInlineRuns.Rank(text.Marks[index]) == rank)
                    {
                        WriteMark(writer, text.Marks[index]);
                    }
                }
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    private static void WriteMark(Utf8JsonWriter writer, AdfMark mark)
    {
        switch (mark)
        {
            case AdfStrongMark:
                WriteEmpty(writer, AdfMarkTypes.Strong);

                break;
            case AdfEmphasisMark:
                WriteEmpty(writer, AdfMarkTypes.Emphasis);

                break;
            case AdfStrikeMark:
                WriteEmpty(writer, AdfMarkTypes.Strike);

                break;
            case AdfCodeMark:
                WriteEmpty(writer, AdfMarkTypes.Code);

                break;
            case AdfUnderlineMark:
                WriteEmpty(writer, AdfMarkTypes.Underline);

                break;
            case AdfLinkMark link:
                WriteStart(writer, AdfMarkTypes.Link);
                WriteOptionalAttribute(writer, AdfAttributeNames.Href, link.Href);
                writer.WriteEndObject();

                break;
            case AdfTextColorMark color:
                WriteStart(writer, AdfMarkTypes.TextColor);
                WriteOptionalAttribute(writer, AdfAttributeNames.Color, color.Color);
                writer.WriteEndObject();

                break;
            case AdfSubSupMark subSup:
                WriteStart(writer, AdfMarkTypes.SubSup);
                WriteOptionalAttribute(writer, AdfPropertyNames.Type, subSup.Kind);
                writer.WriteEndObject();

                break;
            case AdfUnknownMark unknown:
                WriteEmpty(writer, unknown.TypeName);

                break;
        }
    }

    private sealed class WriteState(Utf8JsonWriter writer)
    {
        public Utf8JsonWriter Writer { get; } = writer;

        public int LocalIdCount { get; set; }
    }
}
