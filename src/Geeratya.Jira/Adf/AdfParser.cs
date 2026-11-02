using System.Globalization;
using System.Text.Json;

namespace Geeratya.Jira.Adf;

/// <summary>
/// Parses Atlassian Document Format JSON into the <see cref="AdfDocument" /> tree. Tolerant: wrong types, missing
/// members, <c>null</c> and unknown nodes or marks never throw. Nesting deeper than <see cref="MaxDepth" /> is dropped
/// and flagged as truncated, so recursion stays bounded.
/// </summary>
public static class AdfParser
{
    /// <summary>Deepest node level that is parsed; the document itself is level 0.</summary>
    public const int MaxDepth = 64;

    /// <summary>Deepest JSON nesting the reader accepts (a 1000-level document must not crash).</summary>
    private const int MaxJsonDepth = 10_000;

    private const string TypeProperty = "type";
    private const string ContentProperty = "content";
    private const string AttrsProperty = "attrs";
    private const string MarksProperty = "marks";
    private const string TextProperty = "text";
    private const string VersionProperty = "version";
    private const string TaskDoneState = "DONE";
    private const string DefaultPanelType = "info";
    private const int DefaultHeadingLevel = 1;
    private const int MaxHeadingLevel = 6;
    private const int DefaultOrder = 1;
    private const int DefaultSpan = 1;
    private const int DefaultVersion = 1;

    private static readonly JsonDocumentOptions _documentOptions = new() { MaxDepth = MaxJsonDepth };

    public static AdfDocument Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AdfDocument.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(json, _documentOptions);

            return Parse(document.RootElement);
        }
        catch (JsonException)
        {
            return Unreadable();
        }
    }

    public static AdfDocument Parse(ReadOnlySpan<byte> utf8Json)
    {
        if (utf8Json.IsEmpty)
        {
            return AdfDocument.Empty;
        }

        try
        {
            var reader = new Utf8JsonReader(utf8Json, new JsonReaderOptions { MaxDepth = MaxJsonDepth });
            using var document = JsonDocument.ParseValue(ref reader);

            return Parse(document.RootElement);
        }
        catch (JsonException)
        {
            return Unreadable();
        }
    }

    /// <summary>
    /// Parses an element such as <c>fields.description</c> or a comment <c>body</c>; null gives an empty one.
    /// A caller that parses the whole response with default <see cref="JsonDocumentOptions" /> (depth 64) fails
    /// before this method on very deep input; pass the raw text or bytes to the other overloads for that.
    /// </summary>
    public static AdfDocument Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return AdfDocument.Empty;
        }

        var state = new ParseState();
        var version = DefaultVersion;

        if (element.TryGetProperty(VersionProperty, out var versionElement)
            && versionElement.ValueKind == JsonValueKind.Number
            && versionElement.TryGetInt32(out var parsedVersion))
        {
            version = parsedVersion;
        }

        if (GetString(element, TypeProperty) == AdfNodeTypes.Doc)
        {
            var content = ParseChildren(element, 1, state);

            return new AdfDocument(version, content, state.IsTruncated);
        }

        // Not wrapped in a doc: tolerate a bare node, but an object without a type is not ADF.
        if (!element.TryGetProperty(TypeProperty, out _))
        {
            return AdfDocument.Empty;
        }

        var node = ParseNode(element, 1, state);
        IReadOnlyList<AdfNode> single = node is null ? [] : [node];

        return new AdfDocument(version, single, state.IsTruncated);
    }

    private static AdfDocument Unreadable()
    {
        return new AdfDocument(DefaultVersion, [], true);
    }

    private static List<AdfNode> ParseChildren(JsonElement parent, int depth, ParseState state)
    {
        var children = new List<AdfNode>();

        if (!parent.TryGetProperty(ContentProperty, out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return children;
        }

        if (depth > MaxDepth)
        {
            if (content.GetArrayLength() > 0)
            {
                state.IsTruncated = true;
            }

            return children;
        }

        foreach (var child in content.EnumerateArray())
        {
            var node = ParseNode(child, depth, state);

            if (node is not null)
            {
                children.Add(node);
            }
        }

        return children;
    }

    private static AdfNode? ParseNode(JsonElement element, int depth, ParseState state)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var type = GetString(element, TypeProperty) ?? string.Empty;
        var attributes = GetAttributes(element);

        switch (type)
        {
            case AdfNodeTypes.Text:
                return new AdfText(GetString(element, TextProperty) ?? string.Empty, ParseMarks(element));
            case AdfNodeTypes.HardBreak:
                return new AdfHardBreak();
            case AdfNodeTypes.Rule:
                return new AdfRule();
            case AdfNodeTypes.InlineCard:
                return new AdfInlineCard(GetString(attributes, AdfAttributeNames.Url));
            case AdfNodeTypes.Emoji:
                return new AdfEmoji(
                    GetString(attributes, AdfAttributeNames.ShortName),
                    GetString(attributes, TextProperty));
            case AdfNodeTypes.Date:
                return new AdfDate(GetLong(attributes, AdfAttributeNames.Timestamp));
            case AdfNodeTypes.Status:
                return new AdfStatus(
                    GetString(attributes, TextProperty) ?? string.Empty,
                    GetString(attributes, AdfAttributeNames.Color));
            case AdfNodeTypes.Mention:
                return new AdfMention(GetString(attributes, AdfAttributeNames.Id), GetString(attributes, TextProperty));
            case AdfNodeTypes.Media:
                return new AdfMedia(
                    GetString(attributes, AdfAttributeNames.Id),
                    GetString(attributes, TypeProperty),
                    GetString(attributes, AdfAttributeNames.Collection),
                    GetString(attributes, AdfAttributeNames.Alt),
                    GetInt(attributes, AdfAttributeNames.Width),
                    GetInt(attributes, AdfAttributeNames.Height));
            case AdfNodeTypes.MediaInline:
                return new AdfMediaInline(
                    GetString(attributes, AdfAttributeNames.Id),
                    GetString(attributes, TypeProperty),
                    GetString(attributes, AdfAttributeNames.Collection),
                    GetString(attributes, AdfAttributeNames.Alt),
                    GetInt(attributes, AdfAttributeNames.Width),
                    GetInt(attributes, AdfAttributeNames.Height));
        }

        var children = ParseChildren(element, depth + 1, state);

        return type switch
        {
            AdfNodeTypes.Paragraph => new AdfParagraph(children),
            AdfNodeTypes.Heading => new AdfHeading(ParseHeadingLevel(attributes), children),
            AdfNodeTypes.BulletList => new AdfBulletList(children),
            AdfNodeTypes.OrderedList => new AdfOrderedList(
                GetInt(attributes, AdfAttributeNames.Order) ?? DefaultOrder,
                children),
            AdfNodeTypes.ListItem => new AdfListItem(children),
            AdfNodeTypes.TaskList => new AdfTaskList(children),
            AdfNodeTypes.TaskItem => new AdfTaskItem(ParseTaskState(attributes), children),
            AdfNodeTypes.CodeBlock => new AdfCodeBlock(GetString(attributes, AdfAttributeNames.Language), children),
            AdfNodeTypes.Blockquote => new AdfBlockquote(children),
            AdfNodeTypes.Panel => new AdfPanel(
                GetString(attributes, AdfAttributeNames.PanelType) ?? DefaultPanelType,
                children),
            AdfNodeTypes.Table => new AdfTable(children),
            AdfNodeTypes.TableRow => new AdfTableRow(children),
            AdfNodeTypes.TableHeader => new AdfTableHeader(
                ParseSpan(attributes, AdfAttributeNames.ColumnSpan),
                children)
            {
                RowSpan = ParseSpan(attributes, AdfAttributeNames.RowSpan),
                ColumnWidths = ParseColumnWidths(attributes),
            },
            AdfNodeTypes.TableCell => new AdfTableCell(
                ParseSpan(attributes, AdfAttributeNames.ColumnSpan),
                children)
            {
                RowSpan = ParseSpan(attributes, AdfAttributeNames.RowSpan),
                ColumnWidths = ParseColumnWidths(attributes),
            },
            AdfNodeTypes.Expand => new AdfExpand(GetString(attributes, AdfAttributeNames.Title), children),
            AdfNodeTypes.NestedExpand => new AdfNestedExpand(GetString(attributes, AdfAttributeNames.Title), children),
            AdfNodeTypes.DecisionList => new AdfDecisionList(children),
            AdfNodeTypes.DecisionItem => new AdfDecisionItem(GetString(attributes, AdfAttributeNames.State), children),
            AdfNodeTypes.Caption => new AdfCaption(children),
            AdfNodeTypes.MediaSingle => new AdfMediaSingle(GetString(attributes, AdfAttributeNames.Layout), children),
            AdfNodeTypes.MediaGroup => new AdfMediaGroup(children),
            _ => new AdfUnknownNode(
                type,
                GetString(element, TextProperty) ?? GetString(attributes, TextProperty),
                children),
        };
    }

    private static List<AdfMark> ParseMarks(JsonElement node)
    {
        var marks = new List<AdfMark>();

        if (!node.TryGetProperty(MarksProperty, out var markArray) || markArray.ValueKind != JsonValueKind.Array)
        {
            return marks;
        }

        foreach (var markElement in markArray.EnumerateArray())
        {
            if (markElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var type = GetString(markElement, TypeProperty) ?? string.Empty;
            var attributes = GetAttributes(markElement);

            marks.Add(
                type switch
                {
                    AdfMarkTypes.Strong => new AdfStrongMark(),
                    AdfMarkTypes.Emphasis => new AdfEmphasisMark(),
                    AdfMarkTypes.Strike => new AdfStrikeMark(),
                    AdfMarkTypes.Code => new AdfCodeMark(),
                    AdfMarkTypes.Underline => new AdfUnderlineMark(),
                    AdfMarkTypes.Link => new AdfLinkMark(GetString(attributes, AdfAttributeNames.Href)),
                    AdfMarkTypes.TextColor => new AdfTextColorMark(GetString(attributes, AdfAttributeNames.Color)),
                    AdfMarkTypes.SubSup => new AdfSubSupMark(GetString(attributes, TypeProperty)),
                    _ => new AdfUnknownMark(type),
                });
        }

        return marks;
    }

    private static JsonElement GetAttributes(JsonElement element)
    {
        if (element.TryGetProperty(AttrsProperty, out var attributes) && attributes.ValueKind == JsonValueKind.Object)
        {
            return attributes;
        }

        return default;
    }

    private static string? GetString(JsonElement element, string name)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        return null;
    }

    private static int? GetInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static long? GetLong(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
        {
            return number;
        }

        if (value.ValueKind == JsonValueKind.String
            && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
        {
            return number;
        }

        return null;
    }

    private static int ParseHeadingLevel(JsonElement attributes)
    {
        var level = GetInt(attributes, AdfAttributeNames.Level) ?? DefaultHeadingLevel;

        return Math.Clamp(level, DefaultHeadingLevel, MaxHeadingLevel);
    }

    private static int ParseSpan(JsonElement attributes, string name)
    {
        var span = GetInt(attributes, name) ?? DefaultSpan;

        return Math.Max(span, DefaultSpan);
    }

    /// <summary>
    /// Reads <c>colwidth</c>, an array of numbers (one per spanned column). A non-array, or any entry that is not a
    /// whole number, gives an empty list so a reader never sees a partial or misaligned set of widths.
    /// </summary>
    private static List<int> ParseColumnWidths(JsonElement attributes)
    {
        if (attributes.ValueKind != JsonValueKind.Object
            || !attributes.TryGetProperty(AdfAttributeNames.ColumnWidth, out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var widths = new List<int>(array.GetArrayLength());

        foreach (var entry in array.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Number && entry.TryGetInt32(out var width) && width >= 0)
            {
                widths.Add(width);
            }
            else if (entry.ValueKind == JsonValueKind.Number && entry.TryGetDouble(out var fraction)
                && fraction is >= 0 and <= int.MaxValue)
            {
                widths.Add((int)Math.Round(fraction));
            }
            else
            {
                return [];
            }
        }

        return widths;
    }

    private static AdfTaskState ParseTaskState(JsonElement attributes)
    {
        return GetString(attributes, AdfAttributeNames.State) == TaskDoneState ? AdfTaskState.Done : AdfTaskState.Todo;
    }

    private sealed class ParseState
    {
        public bool IsTruncated { get; set; }
    }
}
