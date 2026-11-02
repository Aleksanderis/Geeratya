using System.Text.Json;
using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;
using static Geeratya.Core.Tests.Adf.AdfTestSupport;

namespace Geeratya.Core.Tests.Adf;

public sealed class AdfUnknownAndMalformedTests
{
    [Fact]
    public void GivenOnlyUnknownNodes_WhenRendering_ThenEachShowsAMarkerAndItsText()
    {
        const string json = """
            {"type":"placeholder","attrs":{"text":"Type here"}},
            {"type":"layoutSection","content":[{"type":"paragraph","content":[{"type":"text","text":"inside"}]}]}
            """;

        var expected = new[]
        {
            "[unsupported: placeholder] Type here",
            string.Empty,
            "[unsupported: layoutSection]",
            "inside",
        };

        Assert.Equal(expected, Lines(json, 80));
    }

    [Fact]
    public void GivenUnknownInlineNodeInParagraph_WhenRendering_ThenMarkerAndTextStayInTheLine()
    {
        const string json = """
            {"type":"paragraph","content":[
              {"type":"text","text":"a "},
              {"type":"inlineExtension","attrs":{"text":"ext"}},
              {"type":"text","text":" b"}]}
            """;

        Assert.Equal(["a [unsupported: inlineExtension] ext b"], Lines(json, 80));
    }

    [Fact]
    public void GivenUnknownMark_WhenRendering_ThenTheTextIsKeptBehindAMarker()
    {
        const string json = """
            {"type":"paragraph","content":[{"type":"text","text":"hi","marks":[{"type":"annotation"}]}]}
            """;

        Assert.Equal(["[unsupported: annotation] hi"], Lines(json, 80));
    }

    [Fact]
    public void GivenUnknownNode_WhenRendering_ThenTheMarkerIsMuted()
    {
        var spans = Spans("""{"type":"mystery","content":[]}""", 80);

        Assert.Equal(TextRole.Muted, Assert.Single(spans).Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{")]
    [InlineData("""{"type":"doc","content":"nope"}""")]
    [InlineData("""{"type":"doc","content":[1,null,"x",{"type":5},{"content":[{}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"table","content":[{"type":"tableRow"},{"type":"paragraph"}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"bulletList","content":[{"type":"paragraph"}]}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"emoji"},{"type":"date","attrs":{"timestamp":"x"}}]}""")]
    [InlineData("""{"type":"doc","content":[{"type":"paragraph","content":[{"type":"status"}]}]}""")]
    [InlineData("""
        {"type":"doc","content":[{"type":"paragraph","content":[
          {"type":"date","attrs":{"timestamp":"99999999999999999"}}]}]}
        """)]
    public void GivenMalformedJson_WhenParsingAndRendering_ThenNothingThrows(string json)
    {
        var document = AdfParser.Parse(json);

        foreach (var width in new[] { 1, 5, 80 })
        {
            var rendering = AdfRenderer.Render(document, width);

            Assert.NotNull(rendering.Lines);
        }
    }

    [Fact]
    public void GivenNestingBeyondTheParserLimit_WhenRendering_ThenTruncationIsMarked()
    {
        var json = DeepQuote(AdfParser.MaxDepth + 20);

        var rendering = AdfRenderer.Render(AdfParser.Parse(json), 120);

        Assert.True(AdfParser.Parse(json).IsTruncated);
        Assert.Equal("[unsupported: nesting too deep]", PlainLines(rendering)[^1]);
    }

    [Fact]
    public void GivenHandBuiltTreeDeeperThanTheRendererLimit_WhenRendering_ThenItStopsWithAMarker()
    {
        AdfNode node = new AdfParagraph([new AdfText("leaf", [])]);

        for (var level = 0; level < 500; level++)
        {
            node = new AdfBlockquote([node]);
        }

        var rendering = AdfRenderer.Render(new AdfDocument(1, [node], false), 2000);

        Assert.Contains(PlainLines(rendering), static line => line.EndsWith("[unsupported: nesting too deep]"));
    }

    [Fact]
    public void GivenHandBuiltInlineTreeDeeperThanTheLimit_WhenRendering_ThenItDoesNotThrow()
    {
        AdfNode node = new AdfText("leaf", []);

        for (var level = 0; level < 5000; level++)
        {
            node = new AdfUnknownNode("deep", null, [node]);
        }

        var rendering = AdfRenderer.Render(new AdfDocument(1, [new AdfParagraph([node])], false), 80);

        Assert.True(rendering.LineCount > 0);
    }

    [Fact]
    public void GivenNodesWithMissingTextAndAttributes_WhenRendering_ThenTheyRenderWithoutText()
    {
        var document = new AdfDocument(
            1,
            [
                new AdfParagraph([new AdfInlineCard(null), new AdfMention(null, null), new AdfDate(null)]),
                new AdfCodeBlock(null, []),
                new AdfExpand(null, []),
                new AdfPanel(string.Empty, []),
            ],
            false);

        var lines = PlainLines(AdfRenderer.Render(document, 40));

        Assert.Contains("[link]@unknown", lines);
        Assert.Contains("v Expand", lines);
    }

    [Fact]
    public void GivenRecordedComments_WhenRendering_ThenEveryBodyRendersAtFourWidths()
    {
        using var file = JsonDocument.Parse(FixtureJson("comments-gtm5.json"));
        var bodies = file.RootElement.GetProperty("comments").EnumerateArray()
            .Select(static comment => AdfParser.Parse(comment.GetProperty("body")))
            .ToList();

        Assert.NotEmpty(bodies);

        foreach (var width in new[] { 20, 40, 80, 120 })
        {
            Assert.All(bodies, body => Assert.True(AdfRenderer.Render(body, width).LineCount > 0));
        }
    }

    [Fact]
    public void GivenRecordedRichDescription_WhenRendering_ThenNoUnsupportedMarkerAppears()
    {
        var lines = PlainLines(AdfRenderer.Render(FixtureDescription(RichDescription), 80));

        Assert.DoesNotContain(lines, static line => line.Contains("[unsupported"));
    }

    private static string DeepQuote(int levels)
    {
        var open = string.Concat(Enumerable.Repeat("""{"type":"blockquote","content":[""", levels));
        var closed = string.Concat(Enumerable.Repeat("]}", levels));

        return $$"""{"version":1,"type":"doc","content":[{{open}}{{Paragraph("x")}}{{closed}}]}""";
    }
}
