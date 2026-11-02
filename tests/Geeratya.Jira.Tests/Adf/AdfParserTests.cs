using System.Text;
using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Adf;

public sealed class AdfParserTests
{
    private const string RichDescription = "adf/issue-gtm5-rich-description.json";
    private const string Comments = "adf/comments-gtm5.json";
    private const string ExtendedNodes = "adf/issue-gcm-extended-nodes.json";

    [Fact]
    public void GivenRichDescriptionFixture_WhenParsing_ThenEveryNodeTypeIsTyped()
    {
        var document = AdfParser.Parse(FieldElement(RichDescription, "description"));
        var nodes = AllNodes(document.Content);

        Assert.False(document.IsTruncated);
        Assert.Equal(1, document.Version);
        Assert.Empty(nodes.OfType<AdfUnknownNode>());
        AssertPresent<AdfParagraph>(nodes);
        AssertPresent<AdfHeading>(nodes);
        AssertPresent<AdfText>(nodes);
        AssertPresent<AdfHardBreak>(nodes);
        AssertPresent<AdfBulletList>(nodes);
        AssertPresent<AdfOrderedList>(nodes);
        AssertPresent<AdfListItem>(nodes);
        AssertPresent<AdfTaskList>(nodes);
        AssertPresent<AdfTaskItem>(nodes);
        AssertPresent<AdfCodeBlock>(nodes);
        AssertPresent<AdfBlockquote>(nodes);
        AssertPresent<AdfPanel>(nodes);
        AssertPresent<AdfRule>(nodes);
        AssertPresent<AdfTable>(nodes);
        AssertPresent<AdfTableRow>(nodes);
        AssertPresent<AdfTableHeader>(nodes);
        AssertPresent<AdfTableCell>(nodes);
        AssertPresent<AdfExpand>(nodes);
        AssertPresent<AdfInlineCard>(nodes);
        AssertPresent<AdfEmoji>(nodes);
        AssertPresent<AdfDate>(nodes);
        AssertPresent<AdfStatus>(nodes);
    }

    [Fact]
    public void GivenRichDescriptionFixture_WhenParsing_ThenAttributesAreKept()
    {
        var nodes = AllNodes(AdfParser.Parse(FieldElement(RichDescription, "description")).Content);

        Assert.Equal([1, 3], nodes.OfType<AdfHeading>().Select(heading => heading.Level).Distinct().Order());
        Assert.Contains(nodes.OfType<AdfOrderedList>(), list => list.Order == 3);
        Assert.Contains(nodes.OfType<AdfTaskItem>(), item => item.State == AdfTaskState.Done);
        Assert.Contains(nodes.OfType<AdfTaskItem>(), item => item.State == AdfTaskState.Todo);
        Assert.Contains(nodes.OfType<AdfCodeBlock>(), block => !string.IsNullOrEmpty(block.Language));
        Assert.Contains(nodes.OfType<AdfPanel>(), panel => panel.PanelType.Length > 0);
        Assert.Contains(nodes.OfType<AdfExpand>(), expand => !string.IsNullOrEmpty(expand.Title));
        Assert.Contains(nodes.OfType<AdfInlineCard>(), card => card.Url?.StartsWith("https://") == true);
        Assert.Contains(nodes.OfType<AdfEmoji>(), emoji => emoji is { ShortName: ":smile:", Text: "\U0001F604" });
        Assert.Contains(nodes.OfType<AdfDate>(), date => date.TimestampMilliseconds == 1767225600000);
        Assert.Contains(nodes.OfType<AdfStatus>(), status => status.Text.Length > 0 && status.Color is not null);
        Assert.Contains(nodes.OfType<AdfTableCell>(), cell => cell.ColumnSpan >= 1);
    }

    [Fact]
    public void GivenRichDescriptionFixture_WhenParsing_ThenEveryMarkIsTyped()
    {
        var nodes = AllNodes(AdfParser.Parse(FieldElement(RichDescription, "description")).Content);
        var marks = nodes.OfType<AdfText>().SelectMany(text => text.Marks).ToList();

        Assert.Empty(marks.OfType<AdfUnknownMark>());
        AssertPresent<AdfStrongMark>(marks);
        AssertPresent<AdfEmphasisMark>(marks);
        AssertPresent<AdfStrikeMark>(marks);
        AssertPresent<AdfCodeMark>(marks);
        AssertPresent<AdfUnderlineMark>(marks);
        Assert.Contains(marks.OfType<AdfLinkMark>(), link => link.Href == "https://example.com/page");
        Assert.Contains(marks.OfType<AdfTextColorMark>(), color => color.Color == "#ff5630");
        Assert.Contains(marks.OfType<AdfSubSupMark>(), subSup => subSup.Kind == "sub");
    }

    [Fact]
    public void GivenRichDescriptionFixture_WhenParsing_ThenUnicodeAndEmojiTextSurvive()
    {
        var nodes = AllNodes(AdfParser.Parse(FieldElement(RichDescription, "description")).Content);

        Assert.Contains(nodes.OfType<AdfText>(), text => text.Text.Contains("żółć 日本語 🚀"));
    }

    [Fact]
    public void GivenUnknownNodeWithTextOnlyInAttributes_WhenParsing_ThenThatTextIsKept()
    {
        const string json = """{"type":"doc","content":[{"type":"placeholder","attrs":{"text":"Type here"}}]}""";

        var parsed = AdfParser.Parse(json);

        var unknown = Assert.IsType<AdfUnknownNode>(Assert.Single(parsed.Content));
        Assert.Equal("placeholder", unknown.TypeName);
        Assert.Equal("Type here", unknown.Text);
    }

    [Fact]
    public void GivenCommentsFixture_WhenParsingEveryBody_ThenNothingIsUnknown()
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read(Comments));
        var bodies = document.RootElement.GetProperty("comments").EnumerateArray().ToList();

        Assert.NotEmpty(bodies);

        foreach (var comment in bodies)
        {
            var parsed = AdfParser.Parse(comment.GetProperty("body"));

            Assert.NotEmpty(parsed.Content);
            Assert.Empty(AllNodes(parsed.Content).OfType<AdfUnknownNode>());
        }
    }

    [Fact]
    public void GivenMentionCommentFixture_WhenParsing_ThenMentionKeepsIdAndVisibleText()
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read("adf/comment-post-mention-gtm5.json"));

        var parsed = AdfParser.Parse(document.RootElement.GetProperty("body"));

        var mention = Assert.Single(AllNodes(parsed.Content).OfType<AdfMention>());
        Assert.Equal("fake-account-1", mention.Id);
        Assert.Equal("@Test User 1", mention.Text);
    }

    [Fact]
    public void GivenRichCommentFixture_WhenParsing_ThenItHasNoUnknownNodesOrMarks()
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read("adf/comment-post-rich-gtm5.json"));

        var parsed = AdfParser.Parse(document.RootElement.GetProperty("body"));

        var nodes = AllNodes(parsed.Content);
        Assert.Empty(nodes.OfType<AdfUnknownNode>());
        Assert.Empty(nodes.OfType<AdfText>().SelectMany(text => text.Marks).OfType<AdfUnknownMark>());
    }

    [Fact]
    public void GivenMediaFixture_WhenParsing_ThenMediaIsKeptAsNodeWithoutResolving()
    {
        var parsed = AdfParser.Parse(FieldElement("adf/issue-gcm-media-description.json", "description"));

        var single = Assert.Single(AllNodes(parsed.Content).OfType<AdfMediaSingle>());
        Assert.Equal("center", single.Layout);
        var media = Assert.IsType<AdfMedia>(Assert.Single(single.Children));
        Assert.Equal("file", media.MediaType);
        Assert.False(string.IsNullOrEmpty(media.Id));
        Assert.Equal(string.Empty, media.Collection);
        Assert.Null(media.Alt);
        Assert.Equal(200, media.Width);
        Assert.Equal(183, media.Height);
    }

    [Fact]
    public void GivenUnknownNodeAndMark_WhenParsing_ThenTypeNamesTextAndChildrenSurvive()
    {
        const string json = """
            {"version":1,"type":"doc","content":[
              {"type":"layoutSection","content":[
                {"type":"paragraph","content":[
                  {"type":"text","text":"kept","marks":[{"type":"strong"},{"type":"annotation","attrs":{"id":"x"}}]}
                ]}]},
              {"type":"futureInline","text":"own text"}
            ]}
            """;

        var parsed = AdfParser.Parse(json);

        var unknown = Assert.IsType<AdfUnknownNode>(parsed.Content[0]);
        Assert.Equal("layoutSection", unknown.TypeName);
        var text = Assert.IsType<AdfText>(Assert.IsType<AdfParagraph>(Assert.Single(unknown.Children)).Children[0]);
        Assert.Equal("kept", text.Text);
        Assert.IsType<AdfStrongMark>(text.Marks[0]);
        Assert.Equal("annotation", Assert.IsType<AdfUnknownMark>(text.Marks[1]).TypeName);
        var inline = Assert.IsType<AdfUnknownNode>(parsed.Content[1]);
        Assert.Equal("own text", inline.Text);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"text\"")]
    [InlineData("42")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("""{"version":1,"type":"doc"}""")]
    [InlineData("""{"version":1,"type":"doc","content":[]}""")]
    [InlineData("""{"version":1,"type":"doc","content":"nope"}""")]
    public void GivenEmptyNullOrWrongShape_WhenParsing_ThenDocumentHasNoContentAndDoesNotThrow(string json)
    {
        var parsed = AdfParser.Parse(json);

        Assert.Empty(parsed.Content);
        Assert.False(parsed.IsTruncated);
    }

    [Fact]
    public void GivenNullDescriptionElement_WhenParsing_ThenDocumentIsEmpty()
    {
        using var document = JsonDocument.Parse("""{"description":null}""");

        var parsed = AdfParser.Parse(document.RootElement.GetProperty("description"));

        Assert.Same(AdfDocument.Empty, parsed);
    }

    [Fact]
    public void GivenDefaultElement_WhenParsing_ThenDocumentIsEmpty()
    {
        Assert.Same(AdfDocument.Empty, AdfParser.Parse(default(JsonElement)));
    }

    [Fact]
    public void GivenMalformedJson_WhenParsing_ThenDocumentIsEmptyAndTruncated()
    {
        var parsed = AdfParser.Parse("{\"type\":\"doc\",");

        Assert.Empty(parsed.Content);
        Assert.True(parsed.IsTruncated);
    }

    [Fact]
    public void GivenWrongTypedMembers_WhenParsing_ThenDefaultsApplyAndNothingThrows()
    {
        const string json = """
            {"type":"doc","content":[
              null, 5, "x",
              {"type":"heading","attrs":{"level":"high"},"content":[{"type":"text"}]},
              {"type":"orderedList","attrs":{"order":"7"},"content":{}},
              {"type":"orderedList","attrs":[]},
              {"type":"taskItem","attrs":{"state":5}},
              {"type":"tableCell","attrs":{"colspan":"2"}},
              {"type":"tableHeader","attrs":{"colspan":-4}},
              {"type":"date","attrs":{"timestamp":true}},
              {"type":"panel","attrs":{"panelType":3}},
              {"type":"paragraph","content":[{"type":"text","text":7,"marks":"bold"},{"type":"text","marks":[1,{}]}]},
              {"content":[]}
            ]}
            """;

        var parsed = AdfParser.Parse(json);

        Assert.Equal(1, Assert.IsType<AdfHeading>(parsed.Content[0]).Level);
        var headingChildren = ((AdfHeading)parsed.Content[0]).Children;
        Assert.Equal(string.Empty, Assert.IsType<AdfText>(Assert.Single(headingChildren)).Text);
        var ordered = Assert.IsType<AdfOrderedList>(parsed.Content[1]);
        Assert.Equal(7, ordered.Order);
        Assert.Empty(ordered.Children);
        Assert.Equal(1, Assert.IsType<AdfOrderedList>(parsed.Content[2]).Order);
        Assert.Equal(AdfTaskState.Todo, Assert.IsType<AdfTaskItem>(parsed.Content[3]).State);
        Assert.Equal(2, Assert.IsType<AdfTableCell>(parsed.Content[4]).ColumnSpan);
        Assert.Equal(1, Assert.IsType<AdfTableHeader>(parsed.Content[5]).ColumnSpan);
        Assert.Null(Assert.IsType<AdfDate>(parsed.Content[6]).TimestampMilliseconds);
        Assert.Equal("info", Assert.IsType<AdfPanel>(parsed.Content[7]).PanelType);
        var paragraph = Assert.IsType<AdfParagraph>(parsed.Content[8]);
        Assert.Empty(Assert.IsType<AdfText>(paragraph.Children[0]).Marks);
        var secondMarks = ((AdfText)paragraph.Children[1]).Marks;
        Assert.Equal(string.Empty, Assert.IsType<AdfUnknownMark>(Assert.Single(secondMarks)).TypeName);
        Assert.Equal(string.Empty, Assert.IsType<AdfUnknownNode>(parsed.Content[9]).TypeName);
    }

    [Fact]
    public void GivenHeadingLevelOutOfRange_WhenParsing_ThenLevelIsClamped()
    {
        const string json = """
            {"type":"doc","content":[
              {"type":"heading","attrs":{"level":99}},
              {"type":"heading","attrs":{"level":0}}
            ]}
            """;

        var parsed = AdfParser.Parse(json);

        Assert.Equal(6, Assert.IsType<AdfHeading>(parsed.Content[0]).Level);
        Assert.Equal(1, Assert.IsType<AdfHeading>(parsed.Content[1]).Level);
    }

    [Fact]
    public void GivenBareNodeWithoutDoc_WhenParsing_ThenNodeIsWrapped()
    {
        var parsed = AdfParser.Parse("""{"type":"paragraph","content":[{"type":"text","text":"hi"}]}""");

        Assert.IsType<AdfParagraph>(Assert.Single(parsed.Content));
    }

    [Fact]
    public void GivenUtf8Bytes_WhenParsing_ThenResultMatchesStringParse()
    {
        var json = FixtureFiles.Read("adf/comment-post-mention-gtm5.json");
        using var document = JsonDocument.Parse(json);
        var body = document.RootElement.GetProperty("body").GetRawText();

        var fromBytes = AdfParser.Parse(Encoding.UTF8.GetBytes(body));
        var fromString = AdfParser.Parse(body);

        Assert.Equal(AllNodes(fromString.Content).Count, AllNodes(fromBytes.Content).Count);
        Assert.Single(AllNodes(fromBytes.Content).OfType<AdfMention>());
        Assert.Empty(AdfParser.Parse([]).Content);
        Assert.True(AdfParser.Parse("{"u8).IsTruncated);
    }

    [Fact]
    public void GivenThousandLevelNestedDocument_WhenParsing_ThenItIsTruncatedWithoutCrashing()
    {
        var json = NestedBlockquotes(1000);

        var parsed = AdfParser.Parse(json);

        Assert.True(parsed.IsTruncated);
        Assert.Equal(AdfParser.MaxDepth, Depth(parsed.Content));
    }

    [Fact]
    public void GivenThousandLevelNestedElement_WhenParsingElement_ThenItIsTruncatedWithoutCrashing()
    {
        using var document = JsonDocument.Parse(NestedBlockquotes(1000), new JsonDocumentOptions { MaxDepth = 5000 });

        var parsed = AdfParser.Parse(document.RootElement);

        Assert.True(parsed.IsTruncated);
    }

    [Fact]
    public void GivenHundredThousandLevelNesting_WhenParsing_ThenItDoesNotCrash()
    {
        var parsed = AdfParser.Parse(NestedBlockquotes(100_000));

        Assert.True(parsed.IsTruncated);
        Assert.Empty(parsed.Content);
    }

    [Fact]
    public void GivenNestingWithinTheLimit_WhenParsing_ThenItIsNotTruncated()
    {
        var parsed = AdfParser.Parse(NestedBlockquotes(AdfParser.MaxDepth - 1));

        Assert.False(parsed.IsTruncated);
        Assert.Equal(AdfParser.MaxDepth - 1, Depth(parsed.Content));
    }

    [Fact]
    public void GivenExtendedFixture_WhenParsing_ThenNoNodeIsUnknown()
    {
        var document = AdfParser.Parse(FieldElement(ExtendedNodes, "description"));
        var nodes = AllNodes(document.Content);

        Assert.False(document.IsTruncated);
        Assert.Empty(nodes.OfType<AdfUnknownNode>());
        AssertPresent<AdfNestedExpand>(nodes);
        AssertPresent<AdfDecisionList>(nodes);
        AssertPresent<AdfDecisionItem>(nodes);
        AssertPresent<AdfMediaInline>(nodes);
        AssertPresent<AdfCaption>(nodes);
    }

    [Fact]
    public void GivenExtendedFixture_WhenParsing_ThenTableSpansAndWidthsAreKept()
    {
        var nodes = AllNodes(AdfParser.Parse(FieldElement(ExtendedNodes, "description")).Content);

        var wideHeader = Assert.Single(nodes.OfType<AdfTableHeader>(), header => header.ColumnSpan == 2);
        Assert.Equal([100, 150], wideHeader.ColumnWidths);
        Assert.Equal(1, wideHeader.RowSpan);
        var tall = Assert.Single(nodes.OfType<AdfTableCell>(), cell => cell.RowSpan == 2);
        Assert.Equal(1, tall.ColumnSpan);
        Assert.Empty(tall.ColumnWidths);
        Assert.Contains(nodes.OfType<AdfTableCell>(), cell => cell is { ColumnSpan: 2, RowSpan: 1 });
    }

    [Fact]
    public void GivenExtendedFixture_WhenParsing_ThenNestedExpandDecisionCaptionAndInlineMediaKeepTheirContent()
    {
        var nodes = AllNodes(AdfParser.Parse(FieldElement(ExtendedNodes, "description")).Content);

        var nested = Assert.Single(nodes.OfType<AdfNestedExpand>());
        Assert.Equal("Inner", nested.Title);
        var nestedParagraph = Assert.IsType<AdfParagraph>(nested.Children.Single());
        Assert.Equal("nested body", Assert.IsType<AdfText>(nestedParagraph.Children.Single()).Text);
        Assert.Equal("Outer", Assert.Single(nodes.OfType<AdfExpand>()).Title);
        Assert.Equal("DECIDED", Assert.Single(nodes.OfType<AdfDecisionItem>()).State);
        var caption = Assert.Single(nodes.OfType<AdfCaption>());
        Assert.Equal("A caption", Assert.IsType<AdfText>(caption.Children.Single()).Text);
        var inline = Assert.Single(nodes.OfType<AdfMediaInline>());
        Assert.Equal("file", inline.MediaType);
        Assert.Equal("00000000-0000-0000-0000-000000000001", inline.Id);
        Assert.Equal(2, nodes.OfType<AdfMedia>().Count());
    }

    [Theory]
    [InlineData("""{"rowspan":0}""", 1)]
    [InlineData("""{"rowspan":-3}""", 1)]
    [InlineData("""{"rowspan":"3"}""", 3)]
    [InlineData("""{"rowspan":"x"}""", 1)]
    [InlineData("""{"rowspan":null}""", 1)]
    [InlineData("""{"rowspan":2.5}""", 1)]
    [InlineData("""{}""", 1)]
    [InlineData("""{"rowspan":4}""", 4)]
    public void GivenRowSpanAttribute_WhenParsing_ThenInvalidValuesFallBackToOne(string attrs, int expected)
    {
        var (rowSpan, _) = ParseSpans($$"""{"type":"tableCell","attrs":{{attrs}},"content":[]}""");

        Assert.Equal(expected, rowSpan);
    }

    [Theory]
    [InlineData("""[120]""", new[] { 120 })]
    [InlineData("""[0]""", new[] { 0 })]
    [InlineData("""[100.4,200]""", new[] { 100, 200 })]
    [InlineData("""[]""", new int[0])]
    [InlineData("""[100,"x"]""", new int[0])]
    [InlineData("""[100,-1]""", new int[0])]
    [InlineData("""120""", new int[0])]
    [InlineData("""null""", new int[0])]
    public void GivenColumnWidthAttribute_WhenParsing_ThenOnlyAWholeNumericListIsKept(string value, int[] expected)
    {
        var (_, widths) = ParseSpans($$"""{"type":"tableHeader","attrs":{"colwidth":{{value}}},"content":[]}""");

        Assert.Equal(expected, widths);
    }

    [Fact]
    public void GivenTableCellWithoutAttrs_WhenParsing_ThenSpansAreOneAndWidthsEmpty()
    {
        var parsed = AdfParser.Parse("""{"type":"doc","content":[{"type":"tableCell","content":[]}]}""");

        var cell = Assert.IsType<AdfTableCell>(Assert.Single(parsed.Content));
        Assert.Equal(1, cell.ColumnSpan);
        Assert.Equal(1, cell.RowSpan);
        Assert.Empty(cell.ColumnWidths);
    }

    [Fact]
    public void GivenNestedExpandWithoutAttrsAndEmptyDecisionList_WhenParsing_ThenTheyParseWithoutThrowing()
    {
        var parsed = AdfParser.Parse(
            """
            {"type":"doc","content":[
              {"type":"nestedExpand"},
              {"type":"decisionList","content":[{"type":"decisionItem"}]},
              {"type":"mediaInline"},
              {"type":"caption"}]}
            """);

        Assert.Equal(4, parsed.Content.Count);
        Assert.Null(Assert.IsType<AdfNestedExpand>(parsed.Content[0]).Title);
        var list = Assert.IsType<AdfDecisionList>(parsed.Content[1]);
        Assert.Null(Assert.IsType<AdfDecisionItem>(list.Children.Single()).State);
        Assert.Null(Assert.IsType<AdfMediaInline>(parsed.Content[2]).Id);
        Assert.Empty(Assert.IsType<AdfCaption>(parsed.Content[3]).Children);
    }

    private static (int RowSpan, IReadOnlyList<int> Widths) ParseSpans(string cellJson)
    {
        var parsed = AdfParser.Parse($$"""{"type":"doc","content":[{{cellJson}}]}""");

        return Assert.Single(parsed.Content) switch
        {
            AdfTableCell cell => (cell.RowSpan, cell.ColumnWidths),
            AdfTableHeader header => (header.RowSpan, header.ColumnWidths),
            var other => throw new InvalidOperationException(other.GetType().Name),
        };
    }

    private static JsonElement FieldElement(string fixture, string field)
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read(fixture));

        return document.RootElement.GetProperty("fields").GetProperty(field).Clone();
    }

    private static string NestedBlockquotes(int levels)
    {
        var builder = new StringBuilder("{\"version\":1,\"type\":\"doc\",\"content\":");

        for (var level = 0; level < levels; level++)
        {
            builder.Append("[{\"type\":\"blockquote\",\"content\":");
        }

        builder.Append("[{\"type\":\"text\",\"text\":\"deep\"}]");

        for (var level = 0; level < levels; level++)
        {
            builder.Append("}]");
        }

        builder.Append('}');

        return builder.ToString();
    }

    private static int Depth(IReadOnlyList<AdfNode> nodes)
    {
        var depth = 0;
        var current = nodes;

        while (current.Count > 0 && current[0] is AdfContainerNode container)
        {
            depth++;
            current = container.Children;
        }

        return depth;
    }

    private static List<AdfNode> AllNodes(IReadOnlyList<AdfNode> roots)
    {
        var all = new List<AdfNode>();
        var pending = new Stack<AdfNode>(roots);

        while (pending.Count > 0)
        {
            var node = pending.Pop();
            all.Add(node);

            if (node is AdfContainerNode container)
            {
                foreach (var child in container.Children)
                {
                    pending.Push(child);
                }
            }
        }

        return all;
    }

    private static void AssertPresent<T>(IEnumerable<object> items)
    {
        Assert.True(items.OfType<T>().Any(), $"Expected at least one {typeof(T).Name}.");
    }
}
