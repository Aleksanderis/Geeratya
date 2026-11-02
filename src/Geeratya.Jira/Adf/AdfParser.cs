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

    /// <summary>Parses an element such as <c>fields.description</c> or a comment <c>body</c>; null gives an empty one.</summary>
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
                return new AdfInlineCard(GetString(attributes, "url"));
            case AdfNodeTypes.Emoji:
                return new AdfEmoji(GetString(attributes, "shortName"), GetString(attributes, TextProperty));
            case AdfNodeTypes.Date:
                return new AdfDate(GetLong(attributes, "timestamp"));
            case AdfNodeTypes.Status:
                return new AdfStatus(GetString(attributes, TextProperty) ?? string.Empty, GetString(attributes, "color"));
            case AdfNodeTypes.Mention:
                return new AdfMention(GetString(attributes, "id"), GetString(attributes, TextProperty));
            case AdfNodeTypes.Media:
                return new AdfMedia(
                    GetString(attributes, "id"),
                    GetString(attributes, TypeProperty),
                    GetString(attributes, "collection"),
                    GetString(attributes, "alt"),
                    GetInt(attributes, "width"),
                    GetInt(attributes, "height"));
        }

        var children = ParseChildren(element, depth + 1, state);

        return type switch
        {
            AdfNodeTypes.Paragraph => new AdfParagraph(children),
            AdfNodeTypes.Heading => new AdfHeading(ParseHeadingLevel(attributes), children),
            AdfNodeTypes.BulletList => new AdfBulletList(children),
            AdfNodeTypes.OrderedList => new AdfOrderedList(GetInt(attributes, "order") ?? DefaultOrder, children),
            AdfNodeTypes.ListItem => new AdfListItem(children),
            AdfNodeTypes.TaskList => new AdfTaskList(children),
            AdfNodeTypes.TaskItem => new AdfTaskItem(ParseTaskState(attributes), children),
            AdfNodeTypes.CodeBlock => new AdfCodeBlock(GetString(attributes, "language"), children),
            AdfNodeTypes.Blockquote => new AdfBlockquote(children),
            AdfNodeTypes.Panel => new AdfPanel(GetString(attributes, "panelType") ?? DefaultPanelType, children),
            AdfNodeTypes.Table => new AdfTable(children),
            AdfNodeTypes.TableRow => new AdfTableRow(children),
            AdfNodeTypes.TableHeader => new AdfTableHeader(ParseSpan(attributes), children),
            AdfNodeTypes.TableCell => new AdfTableCell(ParseSpan(attributes), children),
            AdfNodeTypes.Expand => new AdfExpand(GetString(attributes, "title"), children),
            AdfNodeTypes.MediaSingle => new AdfMediaSingle(GetString(attributes, "layout"), children),
            AdfNodeTypes.MediaGroup => new AdfMediaGroup(children),
            _ => new AdfUnknownNode(type, GetString(element, TextProperty), children),
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
                    AdfMarkTypes.Link => new AdfLinkMark(GetString(attributes, "href")),
                    AdfMarkTypes.TextColor => new AdfTextColorMark(GetString(attributes, "color")),
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
        var level = GetInt(attributes, "level") ?? DefaultHeadingLevel;

        return Math.Clamp(level, DefaultHeadingLevel, MaxHeadingLevel);
    }

    private static int ParseSpan(JsonElement attributes)
    {
        var span = GetInt(attributes, "colspan") ?? DefaultSpan;

        return Math.Max(span, DefaultSpan);
    }

    private static AdfTaskState ParseTaskState(JsonElement attributes)
    {
        return GetString(attributes, "state") == TaskDoneState ? AdfTaskState.Done : AdfTaskState.Todo;
    }

    private sealed class ParseState
    {
        public bool IsTruncated { get; set; }
    }
}
