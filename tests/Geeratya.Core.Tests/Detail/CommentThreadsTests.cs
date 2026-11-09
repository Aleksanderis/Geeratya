using Geeratya.Core.Detail;
using Geeratya.Core.Text;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>Threaded comments (#897): replies nest under their parent in the Comments pane.</summary>
[Trait("Os", "Independent")]
public class CommentThreadsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    [Fact]
    public void GivenCommentsWithoutParents_WhenArranging_ThenTheLoadedOrderAndDepthZeroStay()
    {
        var result = CommentThreads.Arrange([At("a", 1), At("b", 2), At("c", 3)]);

        Assert.Equal(["a", "b", "c"], result.Select(item => item.Comment.Id));
        Assert.All(result, item => Assert.Equal(0, item.Depth));
    }

    [Fact]
    public void GivenAReplyLoadedAfterOtherComments_WhenArranging_ThenItFollowsItsParent()
    {
        var result = CommentThreads.Arrange([At("a", 1), At("b", 2), At("r", 3, "a")]);

        Assert.Equal(["a", "r", "b"], result.Select(item => item.Comment.Id));
        Assert.Equal([0, 1, 0], result.Select(item => item.Depth));
    }

    [Fact]
    public void GivenNewestFirstOrder_WhenArranging_ThenParentsStayNewestFirstAndRepliesOldestFirst()
    {
        // The loaded order is newest first: p2, then p1 with its replies r2 (newer) and r1 (older).
        var result = CommentThreads.Arrange([At("p2", 9), At("r2", 8, "p1"), At("r1", 7, "p1"), At("p1", 1)]);

        Assert.Equal(["p2", "p1", "r1", "r2"], result.Select(item => item.Comment.Id));
        Assert.Equal([0, 0, 1, 1], result.Select(item => item.Depth));
    }

    [Fact]
    public void GivenAReplyWhoseParentIsNotLoaded_WhenArranging_ThenItShowsFlatInPlace()
    {
        var result = CommentThreads.Arrange([At("a", 1), At("orphan", 2, "gone"), At("b", 3)]);

        Assert.Equal(["a", "orphan", "b"], result.Select(item => item.Comment.Id));
        Assert.All(result, item => Assert.Equal(0, item.Depth));
    }

    [Fact]
    public void GivenAReplyToAReply_WhenArranging_ThenItNestsOneLevelDeeper()
    {
        var result = CommentThreads.Arrange([At("a", 1), At("r", 2, "a"), At("rr", 3, "r")]);

        Assert.Equal([0, 1, 2], result.Select(item => item.Depth));
    }

    [Fact]
    public void GivenALoopAndASelfParent_WhenArranging_ThenEveryCommentIsShownOnce()
    {
        var result = CommentThreads.Arrange([At("x", 1, "y"), At("y", 2, "x"), At("s", 3, "s")]);

        Assert.Equal(3, result.Count);
        Assert.Equal(["s", "x", "y"], result.Select(item => item.Comment.Id).Order());
    }

    [Fact]
    public void GivenAReply_WhenBuildingRows_ThenItsRowsAreIndentedAndKeepTheirTargets()
    {
        var detail = DetailOf([At("a", 1), At("b", 2), At("r", 3, "a")]);

        var rows = CommentRows.Build(detail, 40);

        // Parent a (bar, body, gap), reply r (bar, body, gap), then b.
        Assert.Equal(["a", "r", "b"], rows.Select(row => row.Target?.Value).OfType<string>().Distinct());
        var reply = rows[CommentRows.HeaderOf(rows, "r")];
        Assert.Equal(TextRole.Border, reply.Line.Spans[0].Role);
        Assert.Equal(40, reply.Line.Spans.Sum(span => DisplayWidth.Of(span.Text)));
        Assert.Equal(TextRole.ValueBlock, rows[CommentRows.HeaderOf(rows, "a")].Line.Spans[0].Role);
        Assert.True(reply.IsStop);
        Assert.NotEqual(rows[CommentRows.HeaderOf(rows, "a")].Comment, reply.Comment);
    }

    [Fact]
    public void GivenAReplyBody_WhenReadingItsRows_ThenTheGuideIsABorderSpanSoYankLeavesItOut()
    {
        var rows = CommentRows.Build(DetailOf([At("a", 1), At("r", 2, "a")]), 40);

        var body = rows[CommentRows.HeaderOf(rows, "r") + 1];

        Assert.Equal("│ ", body.Line.Spans[0].Text);
        Assert.Equal(TextRole.Border, body.Line.Spans[0].Role);
        Assert.Equal("text r", DetailText.Of(body.Line));
    }

    private static DetailState DetailOf(IReadOnlyList<IssueComment> comments)
    {
        return DetailState.Opened("A-1", null, 1, 2, 3) with { Comments = comments, LoadedAt = _now, NextComments = null };
    }

    private static IssueComment At(string id, int minutes, string? parent = null)
    {
        var when = _now.AddHours(-5).AddMinutes(minutes);

        return new IssueComment(id, _eve, AdfMarkdownParser.Parse($"text {id}"), when, when, parent);
    }
}
