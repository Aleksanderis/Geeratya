using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using static Geeratya.Core.Tests.Adf.AdfTestSupport;

namespace Geeratya.Core.Tests.Adf;

public sealed class AdfRendererTests
{
    [Theory]
    [InlineData(AdfTestSupport.RichDescription, "rich-description", 20)]
    [InlineData(AdfTestSupport.RichDescription, "rich-description", 40)]
    [InlineData(AdfTestSupport.RichDescription, "rich-description", 80)]
    [InlineData(AdfTestSupport.RichDescription, "rich-description", 120)]
    [InlineData(AdfTestSupport.MediaDescription, "media-description", 20)]
    [InlineData(AdfTestSupport.MediaDescription, "media-description", 40)]
    [InlineData(AdfTestSupport.MediaDescription, "media-description", 80)]
    [InlineData(AdfTestSupport.MediaDescription, "media-description", 120)]
    public void GivenRecordedDescription_WhenRendering_ThenItMatchesTheGoldenText(
        string fixture,
        string golden,
        int width)
    {
        var expected = GoldenText(golden, width);

        var rendering = AdfRenderer.Render(FixtureDescription(fixture), width);

        Assert.Equal(expected, string.Join('\n', PlainLines(rendering)));
    }

    [Fact]
    public void GivenRenderedDocument_WhenReadingLineCount_ThenItIsTheNumberOfLines()
    {
        var rendering = AdfRenderer.Render(Document(Paragraph("one two three four")), 10);

        Assert.Equal(2, rendering.LineCount);
        Assert.Equal(10, rendering.Width);
    }

    [Fact]
    public void GivenSameDocumentAndWidth_WhenRenderingTwice_ThenTheLinesAreEqual()
    {
        var document = FixtureDescription(RichDescription);

        var first = PlainLines(AdfRenderer.Render(document, 40));
        var second = PlainLines(AdfRenderer.Render(document, 40));

        Assert.Equal(first, second);
    }

    [Fact]
    public void GivenEmptyDocument_WhenRendering_ThenThereAreNoLines()
    {
        var rendering = AdfRenderer.Render(Jira.Adf.AdfDocument.Empty, 40);

        Assert.Equal(0, rendering.LineCount);
    }

    [Fact]
    public void GivenHeadingAndParagraph_WhenRendering_ThenHeadingHasItsRoleAndBlocksAreSeparatedByBlankLine()
    {
        const string json = """
            {"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Title"}]},
            {"type":"paragraph","content":[{"type":"text","text":"Body"}]}
            """;

        var rendering = AdfRenderer.Render(Document(json), 40);

        Assert.Equal(["Title", string.Empty, "Body"], PlainLines(rendering));
        Assert.Equal(TextRole.Heading, rendering.Lines[0].Spans[0].Role);
        Assert.Equal(TextRole.Body, rendering.Lines[2].Spans[0].Role);
    }

    [Fact]
    public void GivenEmptyParagraphs_WhenRendering_ThenTheyAddNoBlankLines()
    {
        var json = $$"""{"type":"paragraph"},{{Paragraph("a")}},{"type":"paragraph","content":[]},{{Paragraph("b")}}""";

        Assert.Equal(["a", string.Empty, "b"], Lines(json, 40));
    }

    [Fact]
    public void GivenHardBreak_WhenRendering_ThenTextContinuesOnTheNextLine()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"one"},{"type":"hardBreak"},{"type":"text","text":"two"},{"type":"hardBreak"}]}
            """;

        Assert.Equal(["one", "two"], Lines(json, 40));
    }

    [Fact]
    public void GivenNestedListsThreeDeep_WhenRendering_ThenEachLevelIsIndentedUnderItsMarker()
    {
        const string json = """
            {"type":"bulletList","content":[
              {"type":"listItem","content":[
                {"type":"paragraph","content":[{"type":"text","text":"one"}]},
                {"type":"orderedList","attrs":{"order":9},"content":[
                  {"type":"listItem","content":[
                    {"type":"paragraph","content":[{"type":"text","text":"nine"}]},
                    {"type":"bulletList","content":[
                      {"type":"listItem","content":[
                        {"type":"paragraph","content":[{"type":"text","text":"deep"}]}]}]}]},
                  {"type":"listItem","content":[
                    {"type":"paragraph","content":[{"type":"text","text":"ten"}]}]}]}]},
              {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"two"}]}]}]}
            """;

        var expected = new[]
        {
            "- one",
            "   9. nine",
            "      - deep",
            "  10. ten",
            "- two",
        };

        Assert.Equal(expected, Lines(json, 40));
    }

    [Fact]
    public void GivenLongListItem_WhenRendering_ThenContinuationLinesHangUnderTheText()
    {
        const string json = """
            {"type":"bulletList","content":[{"type":"listItem","content":[
              {"type":"paragraph","content":[{"type":"text","text":"alpha beta gamma delta"}]}]}]}
            """;

        Assert.Equal(["- alpha beta", "  gamma delta"], Lines(json, 14));
    }

    [Fact]
    public void GivenTaskList_WhenRendering_ThenDoneAndTodoItemsGetCheckboxes()
    {
        const string json = """
            {"type":"taskList","content":[
              {"type":"taskItem","attrs":{"state":"DONE"},"content":[{"type":"text","text":"done"}]},
              {"type":"taskItem","attrs":{"state":"TODO"},"content":[{"type":"text","text":"todo"}]}]}
            """;

        Assert.Equal(["[x] done", "[ ] todo"], Lines(json, 40));
    }

    [Fact]
    public void GivenCodeBlock_WhenRendering_ThenLanguageIsShownLinesAreKeptAndLongOnesAreCut()
    {
        const string json = """
            {"type":"codeBlock","attrs":{"language":"cs"},"content":[
              {"type":"text","text":"if (x)\n    return 1234567890;"}]}
            """;

        var rendering = AdfRenderer.Render(Document(json), 12);

        Assert.Equal(["```cs", "if (x)", "    return 1", "234567890;", "```"], PlainLines(rendering));
        Assert.Equal(TextRole.Code, rendering.Lines[2].Spans[0].Role);
    }

    [Fact]
    public void GivenCodeBlockWithoutLanguage_WhenRendering_ThenFencesCarryNoLabel()
    {
        const string json = """{"type":"codeBlock","content":[{"type":"text","text":"x"}]}""";

        Assert.Equal(["```", "x", "```"], Lines(json, 40));
    }

    [Fact]
    public void GivenBlockquote_WhenRendering_ThenEveryLineHasABarAndQuoteRole()
    {
        const string json = """
            {"type":"blockquote","content":[
              {"type":"paragraph","content":[{"type":"text","text":"first"}]},
              {"type":"paragraph","content":[{"type":"text","text":"second"}]}]}
            """;

        var rendering = AdfRenderer.Render(Document(json), 40);

        Assert.Equal(["| first", "|", "| second"], PlainLines(rendering));
        Assert.Equal(TextRole.Quote, rendering.Lines[0].Spans[1].Role);
    }

    [Fact]
    public void GivenPanel_WhenRendering_ThenThePanelTypeWordHeadsTheBarredContent()
    {
        const string json = """
            {"type":"panel","attrs":{"panelType":"info"},"content":[
              {"type":"paragraph","content":[{"type":"text","text":"careful"}]}]}
            """;

        Assert.Equal(["| [info]", "| careful"], Lines(json, 40));
    }

    [Fact]
    public void GivenPanelTypeWithEscapeSequenceAndLongText_WhenRendering_ThenItIsCleanAndWithinTheWidth()
    {
        const string json = """
            {"type":"panel","attrs":{"panelType":"\u001b[2Jvery-long-panel-type-name"},"content":[
              {"type":"paragraph","content":[{"type":"text","text":"x"}]}]}
            """;

        var lines = Lines(json, 12);

        Assert.DoesNotContain(lines, static line => line.Contains('\u001b'));
        Assert.All(lines, static line => Assert.True(line.Length <= 12, line));
    }

    [Fact]
    public void GivenRule_WhenRendering_ThenItIsALineOfDashesNoWiderThanTheWidth()
    {
        Assert.Equal(["----------"], Lines("""{"type":"rule"}""", 10));
        Assert.Equal(40, Lines("""{"type":"rule"}""", 120)[0].Length);
    }

    [Fact]
    public void GivenExpand_WhenRendering_ThenTitleShowsAndContentIsIndentedBelowIt()
    {
        const string json = """
            {"type":"expand","attrs":{"title":"More"},"content":[
              {"type":"paragraph","content":[{"type":"text","text":"inside"}]}]}
            """;

        Assert.Equal(["v More", "  inside"], Lines(json, 40));
    }

    [Fact]
    public void GivenMediaNodes_WhenRendering_ThenTheyShowAsMarkersOnly()
    {
        const string json = """
            {"type":"mediaSingle","content":[{"type":"media","attrs":{"id":"abc","type":"file"}}]},
            {"type":"mediaGroup","content":[
              {"type":"media","attrs":{"id":"a","type":"file"}},
              {"type":"media","attrs":{"id":"b","type":"file","alt":"report.pdf"}}]}
            """;

        Assert.Equal(["[image]", string.Empty, "[file]", "[file] report.pdf"], Lines(json, 40));
    }

    [Fact]
    public void GivenMarks_WhenRendering_ThenEachMarkMapsToItsRoleAndCodeBeatsLink()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"a","marks":[{"type":"strong"}]},
              {"type":"text","text":"b","marks":[{"type":"em"}]},
              {"type":"text","text":"c","marks":[{"type":"strike"}]},
              {"type":"text","text":"d","marks":[{"type":"code"}]},
              {"type":"text","text":"e","marks":[{"type":"underline"}]},
              {"type":"text","text":"f","marks":[{"type":"textColor","attrs":{"color":"#ff0000"}}]}]}
            """;

        var roles = Spans(json, 40).Select(static span => span.Role).ToList();

        Assert.Equal(
            [TextRole.Strong, TextRole.Emphasis, TextRole.Strike, TextRole.Code, TextRole.Emphasis, TextRole.ColorRed],
            roles);
    }

    [Theory]
    [InlineData("#ff5630", TextRole.ColorRed)]
    [InlineData("#f00", TextRole.ColorRed)]
    [InlineData("#ff991f", TextRole.ColorYellow)]
    [InlineData("#36b37e", TextRole.ColorGreen)]
    [InlineData("#00b8d9", TextRole.ColorTeal)]
    [InlineData("#0052cc", TextRole.ColorBlue)]
    [InlineData("#6554c0", TextRole.ColorPurple)]
    [InlineData("#97a0af", TextRole.ColorGray)]
    [InlineData("#E91E63", TextRole.ColorRed)]
    public void GivenTextColour_WhenRendering_ThenItMapsToTheNearestHueRole(string colour, TextRole expected)
    {
        var json = $$$"""
            {"type":"paragraph","content":[
              {"type":"text","text":"x","marks":[{"type":"textColor","attrs":{"color":"{{{colour}}}"}}]}]}
            """;

        Assert.Equal([expected], Spans(json, 40).Select(static span => span.Role));
    }

    [Theory]
    [InlineData("#172b4d")]
    [InlineData("#000000")]
    [InlineData("#ffffff")]
    [InlineData("rebeccapurple")]
    [InlineData("#12")]
    [InlineData("")]
    public void GivenDarkWhiteOrUnreadableColour_WhenRendering_ThenTheTextKeepsItsNormalRole(string colour)
    {
        var json = $$$"""
            {"type":"paragraph","content":[
              {"type":"text","text":"x","marks":[{"type":"textColor","attrs":{"color":"{{{colour}}}"}}]}]}
            """;

        Assert.Equal([TextRole.Body], Spans(json, 40).Select(static span => span.Role));
    }

    [Fact]
    public void GivenStrikeWithColourAndBold_WhenRendering_ThenStrikeWinsOverColourOverBold()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"a","marks":[{"type":"strong"},{"type":"strike"},
                {"type":"textColor","attrs":{"color":"#ff0000"}}]},
              {"type":"text","text":"b","marks":[{"type":"strong"},{"type":"textColor","attrs":{"color":"#ff0000"}}]}]}
            """;

        Assert.Equal([TextRole.Strike, TextRole.ColorRed], Spans(json, 40).Select(static span => span.Role));
    }

    [Fact]
    public void GivenColourInsideHeading_WhenRendering_ThenTheHeadingKeepsItsRole()
    {
        const string json = """
            {"type":"heading","attrs":{"level":1},"content":[
              {"type":"text","text":"x","marks":[{"type":"textColor","attrs":{"color":"#ff0000"}}]}]}
            """;

        Assert.All(Spans(json, 40), static span => Assert.Equal(TextRole.Heading, span.Role));
    }

    [Fact]
    public void GivenLinkWithDifferentText_WhenRendering_ThenTheAddressFollowsOnce()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"see ","marks":[]},
              {"type":"text","text":"the ","marks":[{"type":"link","attrs":{"href":"https://x.test/a"}}]},
              {"type":"text","text":"page","marks":[
                {"type":"link","attrs":{"href":"https://x.test/a"}},{"type":"strong"}]},
              {"type":"text","text":" end"}]}
            """;

        var rendering = AdfRenderer.Render(Document(json), 80);

        Assert.Equal(["see the page <https://x.test/a> end"], PlainLines(rendering));
        Assert.Equal(TextRole.Link, rendering.Lines[0].Spans[1].Role);
    }

    [Fact]
    public void GivenLinkWhoseTextIsTheAddress_WhenRendering_ThenTheAddressIsNotRepeated()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"https://x.test","marks":[{"type":"link","attrs":{"href":"https://x.test"}}]}]}
            """;

        Assert.Equal(["https://x.test"], Lines(json, 80));
    }

    [Fact]
    public void GivenSubscriptAndSuperscript_WhenRendering_ThenTheyGetAMarkerCharacter()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"H"},
              {"type":"text","text":"2","marks":[{"type":"subsup","attrs":{"type":"sub"}}]},
              {"type":"text","text":"O x"},
              {"type":"text","text":"2","marks":[{"type":"subsup","attrs":{"type":"sup"}}]}]}
            """;

        Assert.Equal(["H_2O x^2"], Lines(json, 80));
    }

    [Fact]
    public void GivenInlineNodes_WhenRendering_ThenMentionEmojiDateStatusAndCardAreText()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"mention","attrs":{"id":"x","text":"@Test User 1"}},
              {"type":"text","text":" "},
              {"type":"emoji","attrs":{"shortName":":smile:","text":"😄"}},
              {"type":"text","text":" "},
              {"type":"emoji","attrs":{"shortName":":custom:"}},
              {"type":"text","text":" "},
              {"type":"date","attrs":{"timestamp":"1767225600000"}},
              {"type":"text","text":" "},
              {"type":"status","attrs":{"text":"in progress","color":"blue"}},
              {"type":"text","text":" "},
              {"type":"inlineCard","attrs":{"url":"https://x.test/c"}}]}
            """;

        var expected = "@Test User 1 \U0001F604 :custom: 2026-01-01 [IN PROGRESS] https://x.test/c";

        Assert.Equal([expected], Lines(json, 120));
    }

    [Theory]
    [InlineData("green", TextRole.StatusDone)]
    [InlineData("blue", TextRole.StatusInProgress)]
    [InlineData("neutral", TextRole.StatusToDo)]
    public void GivenStatusColour_WhenRendering_ThenTheChipTakesTheMatchingStatusRole(string color, TextRole role)
    {
        var json = $$$"""
            {"type":"paragraph","content":[{"type":"status","attrs":{"text":"ok","color":"{{{color}}}"}}]}
            """;

        Assert.Equal(role, Assert.Single(Spans(json, 40)).Role);
    }

    [Fact]
    public void GivenBoldInsideHeading_WhenRendering_ThenTheHeadingRoleIsKept()
    {
        const string json = """
            {"type":"heading","attrs":{"level":1},"content":[
              {"type":"text","text":"Big","marks":[{"type":"strong"}]}]}
            """;

        Assert.Equal(TextRole.Heading, Assert.Single(Spans(json, 40)).Role);
    }
}
