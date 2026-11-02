using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Tests.Fakes;

namespace Geeratya.Jira.Tests.Adf;

public sealed class AdfRoundTripTests
{
    private const string Comments = "adf/comments-gtm5.json";
    private const string RichComment = "adf/comment-post-rich-gtm5.json";
    private const string MentionComment = "adf/comment-post-mention-gtm5.json";
    private const string RichDescription = "adf/issue-gtm5-rich-description.json";
    private const string ExtendedNodes = "adf/issue-gcm-extended-nodes.json";
    private const string FuzzAlphabet = "ab *_~+`[]()<>\\#-.1\n@:x\"é";

    [Theory]
    [InlineData("plain text")]
    [InlineData("first\nsecond")]
    [InlineData("one\n\ntwo")]
    [InlineData("# Heading one\n\n### Heading three")]
    [InlineData("**bold** *italic* ~~struck~~ ++under++ `code`")]
    [InlineData("***bold italic***")]
    [InlineData("**bold *and italic* still bold**")]
    [InlineData("[a link](https://example.com/page)")]
    [InlineData("[**bold link**](https://example.com/page) after")]
    [InlineData("**bold [link](https://example.com/page) bold**")]
    [InlineData("see <https://example.com/browse/GCM-1> now")]
    [InlineData("hello @[Test User 1](mention:fake-account-1) from survey")]
    [InlineData("- one\n- two\n- three")]
    [InlineData("1. one\n2. two")]
    [InlineData("3. three\n4. four")]
    [InlineData("- parent\n  - child\n    - grandchild\n- next")]
    [InlineData("1. parent\n   - child\n2. next")]
    [InlineData("- first\n\n  second paragraph\n- next")]
    [InlineData("- [ ] open task\n- [x] done task")]
    [InlineData("```csharp\nvar x = 1;\n\nvar y = 2;\n```")]
    [InlineData("```\nplain\n```")]
    [InlineData("````\n```\ninner\n```\n````")]
    [InlineData("> quoted\n> second line")]
    [InlineData("> quoted\n>\n> - item in quote")]
    [InlineData("---")]
    [InlineData("before\n\n---\n\nafter")]
    [InlineData("Zażółć gęślą jaźń 日本語 🚀 \U0001F604")]
    [InlineData("\\*not bold\\* and \\[not a link\\] and \\`not code\\`")]
    [InlineData("\\# not a heading\n\\- not a list\n1\\. not numbered")]
    [InlineData("2 * 3 * 4 is \\* escaped")]
    [InlineData("C\\+\\+ and a~b and a+b")]
    [InlineData("``a ` b``")]
    [InlineData("[`code link`](https://example.com)")]
    public void GivenCanonicalText_WhenParsedAndWrittenBack_ThenTheTextIsUnchangedAndRoundTrips(string text)
    {
        var document = AdfMarkdownParser.Parse(text);

        Assert.Equal(text, AdfMarkdownWriter.ToText(document));
        Assert.True(AdfRoundTrip.RoundTrips(document));
        var json = AdfJsonWriter.ToJson(document);

        Assert.Equal(json, AdfJsonWriter.ToJson(AdfParser.Parse(json)));
    }

    [Fact]
    public void GivenMarkdownText_WhenParsed_ThenNodesAndMarksAreTyped()
    {
        var document = AdfMarkdownParser.Parse(
            "# Title\n\nSome **bold** and [link](https://x.test/a_b) with @[Ann](mention:acc-1)\n\n- [x] done");

        var heading = Assert.IsType<AdfHeading>(document.Content[0]);
        Assert.Equal(1, heading.Level);
        var paragraph = Assert.IsType<AdfParagraph>(document.Content[1]);
        Assert.Contains(paragraph.Children, node => node is AdfText { Text: "bold", Marks: [AdfStrongMark] });
        Assert.Contains(
            paragraph.Children,
            node => node is AdfText { Text: "link", Marks: [AdfLinkMark { Href: "https://x.test/a_b" }] });
        Assert.Contains(paragraph.Children, node => node is AdfMention { Id: "acc-1", Text: "@Ann" });
        var tasks = Assert.IsType<AdfTaskList>(document.Content[2]);
        Assert.Equal(AdfTaskState.Done, Assert.IsType<AdfTaskItem>(tasks.Children.Single()).State);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\n  ")]
    public void GivenBlankText_WhenParsed_ThenTheDocumentIsEmpty(string text)
    {
        var document = AdfMarkdownParser.Parse(text);

        Assert.Empty(document.Content);
        Assert.True(AdfRoundTrip.RoundTrips(document));
        Assert.Equal(string.Empty, AdfMarkdownWriter.ToText(document));
    }

    [Fact]
    public void GivenNullText_WhenParsed_ThenTheDocumentIsEmpty()
    {
        Assert.Same(AdfDocument.Empty, AdfMarkdownParser.Parse(null));
    }

    [Fact]
    public void GivenWindowsLineEndings_WhenParsed_ThenTheyAreLineBreaks()
    {
        var document = AdfMarkdownParser.Parse("a\r\nb\r\n\r\nc");

        Assert.Equal(2, document.Content.Count);
        Assert.IsType<AdfHardBreak>(Assert.IsType<AdfParagraph>(document.Content[0]).Children[1]);
    }

    [Fact]
    public void GivenUnbalancedDelimitersAndBrokenLinks_WhenParsed_ThenTheyStayText()
    {
        var document = AdfMarkdownParser.Parse("a **b and [c] and [d](  and <e and `f");

        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(document.Content));
        var text = Assert.IsType<AdfText>(Assert.Single(paragraph.Children));
        Assert.Equal("a **b and [c] and [d](  and <e and `f", text.Text);
        Assert.Empty(text.Marks);
    }

    [Fact]
    public void GivenSpacedListMarkerIndentedByTwoUnderAnOrderedItem_WhenParsed_ThenItNestsUnderTheItem()
    {
        var document = AdfMarkdownParser.Parse("1. one\n  - nested\n2. two");

        var list = Assert.IsType<AdfOrderedList>(Assert.Single(document.Content));
        Assert.Equal(2, list.Children.Count);
        Assert.Contains(((AdfListItem)list.Children[0]).Children, node => node is AdfBulletList);
    }

    [Fact]
    public void GivenListWithMixedTaskAndPlainItems_WhenParsed_ThenItIsABulletListWithTheBracketsAsText()
    {
        var document = AdfMarkdownParser.Parse("- [ ] task\n- plain");

        var list = Assert.IsType<AdfBulletList>(Assert.Single(document.Content));
        Assert.Equal(2, list.Children.Count);
        Assert.True(AdfRoundTrip.RoundTrips(document));
    }

    [Fact]
    public void GivenDeepQuoteNesting_WhenParsed_ThenItDoesNotOverflowTheStack()
    {
        var text = string.Concat(Enumerable.Repeat("> ", 5000)) + "deep";

        var document = AdfMarkdownParser.Parse(text);

        Assert.NotEmpty(document.Content);
        Assert.NotNull(AdfMarkdownWriter.ToText(document));
    }

    [Fact]
    public void GivenRecordedComments_WhenChecked_ThenOnlyTheOnesTextCanHoldRoundTrip()
    {
        var plainComment = Body(Comments, 1);
        var richComment = Body(Comments);

        Assert.True(AdfRoundTrip.RoundTrips(plainComment));
        Assert.False(AdfRoundTrip.RoundTrips(richComment));
    }

    [Fact]
    public void GivenRecordedMentionComment_WhenChecked_ThenItRoundTripsAndKeepsTheMention()
    {
        var document = Body(MentionComment);

        Assert.True(AdfRoundTrip.RoundTrips(document));
        Assert.Equal("hello @[Test User 1](mention:fake-account-1) from survey", AdfMarkdownWriter.ToText(document));
    }

    [Theory]
    [InlineData(RichComment)]
    [InlineData(RichDescription)]
    [InlineData(ExtendedNodes)]
    public void GivenRecordedRichDocuments_WhenChecked_ThenTheyDoNotRoundTripButStillGiveText(string fixture)
    {
        var document = fixture == RichDescription || fixture == ExtendedNodes
            ? DescriptionOf(fixture)
            : Body(fixture);

        Assert.False(AdfRoundTrip.RoundTrips(document));
        Assert.NotEmpty(AdfMarkdownWriter.ToText(document));
    }

    [Theory]
    [InlineData("table", """
        {"type":"table","content":[{"type":"tableRow","content":[{"type":"tableCell",
        "content":[{"type":"paragraph","content":[{"type":"text","text":"x"}]}]}]}]}
        """)]
    [InlineData("panel", """
        {"type":"panel","attrs":{"panelType":"info"},
        "content":[{"type":"paragraph","content":[{"type":"text","text":"x"}]}]}
        """)]
    [InlineData("expand", """
        {"type":"expand","attrs":{"title":"T"},"content":[{"type":"paragraph","content":[{"type":"text","text":"x"}]}]}
        """)]
    [InlineData("media", """
        {"type":"mediaSingle","content":[
        {"type":"media","attrs":{"id":"1","type":"file","collection":""}}]}
        """)]
    [InlineData("unknown", """
        {"type":"futureNode","content":[{"type":"paragraph","content":[{"type":"text","text":"x"}]}]}
        """)]
    [InlineData("colour", """
        {"type":"paragraph","content":[{"type":"text","text":"red",
        "marks":[{"type":"textColor","attrs":{"color":"#f00"}}]}]}
        """)]
    [InlineData("status", """
        {"type":"paragraph","content":[{"type":"status","attrs":{"text":"DONE","color":"green"}}]}
        """)]
    [InlineData("date", """
        {"type":"paragraph","content":[{"type":"date","attrs":{"timestamp":"1700000000000"}}]}
        """)]
    [InlineData("subscript", """
        {"type":"paragraph","content":[{"type":"text","text":"2",
        "marks":[{"type":"subsup","attrs":{"type":"sub"}}]}]}
        """)]
    public void GivenNodeTextCannotHold_WhenChecked_ThenItDoesNotRoundTrip(string name, string node)
    {
        var document = AdfParser.Parse($"{{\"version\":1,\"type\":\"doc\",\"content\":[{node}]}}");

        Assert.False(AdfRoundTrip.RoundTrips(document), name);
    }

    [Theory]
    [InlineData("- ")]
    [InlineData("1. ")]
    [InlineData(">")]
    [InlineData("- - a")]
    [InlineData("- # heading")]
    [InlineData("- > quote")]
    [InlineData("> # heading")]
    [InlineData("> > nested")]
    [InlineData("> ---")]
    public void GivenShapesJiraRejects_WhenParsed_ThenOnlyAllowedChildrenRemain(string text)
    {
        var document = AdfMarkdownParser.Parse(text);

        AssertSchemaShape(document.Content, false, false);
    }

    [Fact]
    public void GivenHeadingInQuote_WhenParsed_ThenItBecomesAParagraphWithTheSameText()
    {
        var document = AdfMarkdownParser.Parse("> # note");

        var quote = Assert.IsType<AdfBlockquote>(Assert.Single(document.Content));
        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(quote.Children));
        Assert.Equal("note", Assert.IsType<AdfText>(Assert.Single(paragraph.Children)).Text);
    }

    [Theory]
    [InlineData("[x](javascript:alert(1))", "javascript:alert(1)")]
    [InlineData("[w](https://en.wikipedia.org/wiki/Foo_(bar))", "https://en.wikipedia.org/wiki/Foo_(bar)")]
    public void GivenLinkAddressWithBalancedParentheses_WhenParsed_ThenTheWholeAddressIsKept(string text, string href)
    {
        var document = AdfMarkdownParser.Parse(text);

        var paragraph = Assert.IsType<AdfParagraph>(Assert.Single(document.Content));
        var link = Assert.IsType<AdfText>(Assert.Single(paragraph.Children));
        Assert.Equal(href, Assert.IsType<AdfLinkMark>(Assert.Single(link.Marks)).Href);
        Assert.True(AdfRoundTrip.RoundTrips(document));
    }

    [Fact]
    public void GivenJiraShapedRichDocument_WhenChecked_ThenItRoundTripsFromAdf()
    {
        var document = AdfParser.Parse(
            """
            {"version":1,"type":"doc","content":[
            {"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Plan"}]},
            {"type":"paragraph","content":[{"type":"text","text":"See "},
              {"type":"text","text":"the docs","marks":[{"type":"strong"},
                {"type":"link","attrs":{"href":"https://example.com/a"}}]},
              {"type":"hardBreak"},{"type":"text","text":"next line"}]},
            {"type":"bulletList","content":[{"type":"listItem","content":[
              {"type":"paragraph","content":[{"type":"text","text":"one"}]},
              {"type":"orderedList","attrs":{"order":2},"content":[{"type":"listItem","content":[
                {"type":"paragraph","content":[{"type":"text","text":"deep"}]}]}]}]}]},
            {"type":"codeBlock","attrs":{"language":"csharp"},"content":[{"type":"text","text":"var x = 1;"}]},
            {"type":"blockquote","content":[{"type":"paragraph","content":[{"type":"text","text":"q"}]}]},
            {"type":"rule"}]}
            """);

        Assert.True(AdfRoundTrip.RoundTrips(document));
        Assert.Equal(
            "## Plan\n\nSee [**the docs**](https://example.com/a)\nnext line\n\n- one\n  2. deep\n\n"
            + "```csharp\nvar x = 1;\n```\n\n> q\n\n---",
            AdfMarkdownWriter.ToText(document));
    }

    [Fact]
    public void GivenTruncatedDocument_WhenChecked_ThenItDoesNotRoundTrip()
    {
        Assert.False(AdfRoundTrip.RoundTrips(AdfParser.Parse("{ not json")));
    }

    [Fact]
    public void GivenTextSplitAcrossNodesAndMarksInAnotherOrder_WhenChecked_ThenItRoundTrips()
    {
        var document = AdfParser.Parse(
            "{\"version\":1,\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":["
            + "{\"type\":\"text\",\"text\":\"ab\",\"marks\":[{\"type\":\"em\"},{\"type\":\"strong\"}]},"
            + "{\"type\":\"text\",\"text\":\"cd\",\"marks\":[{\"type\":\"strong\"},{\"type\":\"em\"}]}]},"
            + "{\"type\":\"paragraph\"}]}");

        Assert.True(AdfRoundTrip.RoundTrips(document));
        Assert.Equal("***abcd***", AdfMarkdownWriter.ToText(document));
    }

    [Theory]
    [InlineData("a{0}z")]
    [InlineData("{0}")]
    [InlineData("**{0}**")]
    public void GivenLoneSurrogateTypedIntoText_WhenChecked_ThenItDoesNotRoundTrip(string template)
    {
        // Built here: xUnit would replace a lone surrogate in InlineData with U+FFFD before the test runs.
        foreach (var surrogate in new[] { '\ud83d', '\ude04' })
        {
            var text = template.Replace("{0}", surrogate.ToString(), StringComparison.Ordinal);

            Assert.False(AdfRoundTrip.RoundTrips(AdfMarkdownParser.Parse(text)), template);
        }
    }

    [Fact]
    public void GivenSurrogatePairTypedIntoText_WhenChecked_ThenItRoundTrips()
    {
        Assert.True(AdfRoundTrip.RoundTrips(AdfMarkdownParser.Parse("a\U0001F604z")));
    }

    [Fact]
    public void GivenEmojiNodeWithGlyph_WhenChecked_ThenItRoundTripsAsThePlainCharacter()
    {
        var document = AdfParser.Parse(
            "{\"version\":1,\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":["
            + "{\"type\":\"text\",\"text\":\"hi \"},{\"type\":\"emoji\",\"attrs\":{\"shortName\":\":smile:\","
            + "\"text\":\"\\ud83d\\ude04\"}}]}]}");

        Assert.True(AdfRoundTrip.RoundTrips(document));
        Assert.Equal("hi \U0001F604", AdfMarkdownWriter.ToText(document));
    }

    [Fact]
    public void GivenCodeBlockAndLinkInRichComment_WhenWrittenAsText_ThenTheirTextIsKept()
    {
        var text = AdfMarkdownWriter.ToText(Body(RichComment));

        Assert.Contains("# Heading one", text);
        Assert.Contains("[a link](https://example.com/page)", text);
        Assert.Contains("```", text);
        Assert.Contains("---", text);
    }

    [Fact]
    public void GivenParserOutputOfRandomText_WhenWrittenAndParsedAgain_ThenItIsTheSameDocument()
    {
        // The seed fixes the sequence; the known limits in docs/jira-layer.md (a leading or trailing break, runs of
        // three delimiter characters) are not hit by it, and a failure here is a new kind of difference.
        var random = new Random(161);
        var failures = new List<string>();

        for (var round = 0; round < 4000; round++)
        {
            var length = random.Next(1, 40);
            var characters = new char[length];

            for (var index = 0; index < length; index++)
            {
                characters[index] = FuzzAlphabet[random.Next(FuzzAlphabet.Length)];
            }

            var text = new string(characters);
            var document = AdfMarkdownParser.Parse(text);

            if (!AdfRoundTrip.RoundTrips(document))
            {
                failures.Add($"\"{text.Replace("\n", "\\n")}\"");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} failures, first: {string.Join(" | ", failures.Take(15))}");
    }

    [Fact]
    public void GivenDocumentWithTasksAndOrderedList_WhenWrittenAsJson_ThenItIsValidAdfThatParsesBack()
    {
        var document = AdfMarkdownParser.Parse("3. a\n4. b\n\n- [ ] t1\n- [x] t2\n\n```kotlin\ncode\n```");

        using var json = JsonDocument.Parse(AdfJsonWriter.ToJson(document));
        var content = json.RootElement.GetProperty("content");

        Assert.Equal(1, json.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("doc", json.RootElement.GetProperty("type").GetString());
        Assert.Equal(3, content[0].GetProperty("attrs").GetProperty("order").GetInt32());
        var taskList = content[1];
        Assert.Equal("taskList", taskList.GetProperty("type").GetString());
        Assert.True(taskList.GetProperty("attrs").TryGetProperty("localId", out _));
        var items = taskList.GetProperty("content");
        Assert.Equal("TODO", items[0].GetProperty("attrs").GetProperty("state").GetString());
        Assert.Equal("DONE", items[1].GetProperty("attrs").GetProperty("state").GetString());
        Assert.NotEqual(
            items[0].GetProperty("attrs").GetProperty("localId").GetString(),
            items[1].GetProperty("attrs").GetProperty("localId").GetString());
        Assert.Equal("kotlin", content[2].GetProperty("attrs").GetProperty("language").GetString());
    }

    [Fact]
    public void GivenEmptyTextNodes_WhenWrittenAsJson_ThenTheyAreSkipped()
    {
        var document = new AdfDocument(
            1,
            [new AdfParagraph([new AdfText(string.Empty, []), new AdfText("x", [])])],
            false);

        Assert.Equal(
            "{\"version\":1,\"type\":\"doc\",\"content\":[{\"type\":\"paragraph\",\"content\":"
            + "[{\"type\":\"text\",\"text\":\"x\"}]}]}",
            AdfJsonWriter.ToJson(document));
    }

    private static void AssertSchemaShape(IReadOnlyList<AdfNode> nodes, bool inQuote, bool inItem)
    {
        foreach (var node in nodes)
        {
            Assert.False(inQuote && node is AdfHeading or AdfRule or AdfBlockquote);
            Assert.False(inItem && node is AdfHeading or AdfRule or AdfBlockquote);

            if (node is AdfListItem or AdfBlockquote)
            {
                var children = ((AdfContainerNode)node).Children;

                Assert.NotEmpty(children);
                Assert.True(node is not AdfListItem || children[0] is AdfParagraph);
            }

            if (node is AdfContainerNode container)
            {
                AssertSchemaShape(container.Children, node is AdfBlockquote, node is AdfListItem);
            }
        }
    }

    private static AdfDocument Body(string fixture, int commentIndex = 0)
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read(fixture));
        var root = document.RootElement;
        var comment = root.TryGetProperty("comments", out var comments) ? comments[commentIndex] : root;

        return AdfParser.Parse(comment.GetProperty("body").Clone());
    }

    private static AdfDocument DescriptionOf(string fixture)
    {
        using var document = JsonDocument.Parse(FixtureFiles.Read(fixture));

        return AdfParser.Parse(document.RootElement.GetProperty("fields").GetProperty("description").Clone());
    }
}
