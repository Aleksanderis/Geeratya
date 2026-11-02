using Geeratya.Core.Adf;
using Geeratya.Core.Cache;
using Geeratya.Jira.Adf;
using static Geeratya.Core.Tests.Adf.AdfTestSupport;

namespace Geeratya.Core.Tests.Adf;

/// <summary>
/// Nodes the parser types since #110 must not lose their text or attributes on the way to the screen or the cache.
/// </summary>
public sealed class AdfExtendedNodesTests
{
    private const string ExtendedDescription = "issue-gcm-extended-nodes.json";

    [Fact]
    public void GivenExtendedFixture_WhenRendering_ThenTheTextOfEveryNewNodeIsShownAndNothingIsUnsupported()
    {
        var text = string.Join('\n', PlainLines(AdfRenderer.Render(FixtureDescription(ExtendedDescription), 80)));

        Assert.Contains("nested body", text);
        Assert.Contains("Use ADF", text);
        Assert.Contains("A caption", text);
        Assert.Contains("Inline media", text);
        Assert.DoesNotContain("[unsupported", text);
    }

    [Fact]
    public void GivenExtendedFixture_WhenRendering_ThenRowSpanNestedExpandDecisionInlineMediaAndCaptionHaveTheirLook()
    {
        var lines = PlainLines(AdfRenderer.Render(FixtureDescription(ExtendedDescription), 80));

        var expected = new[]
        {
            "Table with spans",
            "",
            "| Wide header | C             |",
            "|------|------|---------------|",
            "| Tall | Wide                 |",
            "|      | x    | v Inner       |",
            "|      |      |   nested body |",
            "",
            "v Outer",
            "  outer body",
            "",
            "[decided] Use ADF",
            "",
            "Inline media [file]",
            "",
            "[image]",
            "A caption",
        };

        Assert.Equal(expected, lines);
    }

    [Fact]
    public void GivenDecisionItemsWithOtherStates_WhenRendering_ThenTheStateIsShownLowercaseAndWraps()
    {
        const string json = """
            {"type":"decisionList","attrs":{"localId":"a"},"content":[
              {"type":"decisionItem","attrs":{"state":"UNDECIDED"},"content":[{"type":"text","text":"one two three"}]},
              {"type":"decisionItem","content":[{"type":"text","text":"no state"}]}]}
            """;

        Assert.Equal(["[undecided] one two", "            three", "[decision] no state"], Lines(json, 19));
    }

    [Fact]
    public void GivenNestedExpandWithoutTitle_WhenRendering_ThenTheDefaultTitleIsUsed()
    {
        const string json = """
            {"type":"nestedExpand","content":[{"type":"paragraph","content":[{"type":"text","text":"body"}]}]}
            """;

        Assert.Equal(["v Expand", "  body"], Lines(json, 40));
    }

    [Fact]
    public void GivenCaptionWithoutMedia_WhenRendering_ThenTheMarkerStillComesFirst()
    {
        const string json = """
            {"type":"mediaSingle","content":[{"type":"caption","content":[{"type":"text","text":"only caption"}]}]}
            """;

        Assert.Equal(["[image]", "only caption"], Lines(json, 40));
    }

    [Fact]
    public void GivenMediaInlineBetweenText_WhenRenderingInAListItem_ThenItStaysOnTheLine()
    {
        const string json = """
            {"type":"bulletList","content":[{"type":"listItem","content":[{"type":"paragraph","content":[
              {"type":"text","text":"see"},{"type":"mediaInline","attrs":{"id":"x","type":"file"}},
              {"type":"text","text":"here"}]}]}]}
            """;

        Assert.Equal(["- see[file]here"], Lines(json, 40));
    }

    [Fact]
    public void GivenExtendedFixture_WhenCachedAndReadBack_ThenSpansWidthsAndNewNodesSurvive()
    {
        var read = AdfCacheMapper.FromDto(AdfCacheMapper.ToDto(FixtureDescription(ExtendedDescription)));
        var nodes = All(read.Content);

        Assert.Contains(nodes.OfType<AdfTableHeader>(), header => header.ColumnWidths.SequenceEqual([100, 150]));
        Assert.Contains(nodes.OfType<AdfTableCell>(), cell => cell.RowSpan == 2);
        Assert.Single(nodes.OfType<AdfNestedExpand>());
        Assert.Single(nodes.OfType<AdfDecisionItem>(), item => item.State == "DECIDED");
        Assert.Single(nodes.OfType<AdfCaption>());
        Assert.Single(nodes.OfType<AdfMediaInline>());
    }

    [Fact]
    public void GivenCacheFileWrittenBeforeRowSpanExisted_WhenReadBack_ThenSpanIsOneAndWidthsAreEmpty()
    {
        var old = new CachedAdfNode("tableCell", null, null, null, null, null, 2, null, null, null);

        var node = Assert.IsType<AdfTableCell>(AdfCacheMapper.FromDto(
            new CachedAdfDocument(1, false, [old])).Content.Single());

        Assert.Equal(2, node.ColumnSpan);
        Assert.Equal(1, node.RowSpan);
        Assert.Empty(node.ColumnWidths);
    }

    private static List<AdfNode> All(IReadOnlyList<AdfNode> roots)
    {
        var result = new List<AdfNode>();
        var stack = new Stack<AdfNode>(roots.Reverse());

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            result.Add(node);

            if (node is AdfContainerNode container)
            {
                foreach (var child in container.Children.Reverse())
                {
                    stack.Push(child);
                }
            }
        }

        return result;
    }
}
