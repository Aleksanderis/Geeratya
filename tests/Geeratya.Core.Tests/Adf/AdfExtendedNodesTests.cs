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
