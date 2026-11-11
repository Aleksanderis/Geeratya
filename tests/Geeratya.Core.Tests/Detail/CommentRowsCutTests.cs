using Geeratya.Core.Adf;
using Geeratya.Core.Detail;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Detail;

/// <summary>A cut comment wraps only its first lines (#950); the note counts the rest when it is read.</summary>
[Trait("Os", "Independent")]
public class CommentRowsCutTests
{
    private const int Width = 60;

    private static readonly DateTimeOffset _now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly IssueAssignee _eve = new("acc-eve", "Eve Example");

    [Theory]
    [InlineData(1, 3)]
    [InlineData(2, 3)]
    [InlineData(4, 3)]
    [InlineData(30, 3)]
    [InlineData(30, 6)]
    public void GivenAnyLength_WhenRenderingTheHead_ThenItIsThePrefixOfTheFullRendering(int paragraphs, int limit)
    {
        var document = Paragraphs(paragraphs);
        var full = AdfRenderer.Render(document, Width);

        var (head, complete) = AdfRenderer.RenderHead(document, Width, limit);

        var shared = Math.Min(limit, head.LineCount);

        Assert.True(complete || full.LineCount > limit);
        Assert.Equal(PlainOf(full.Lines.Take(shared)), PlainOf(head.Lines.Take(shared)));

        if (complete)
        {
            Assert.Equal(PlainOf(full.Lines), PlainOf(head.Lines));
        }
        else
        {
            Assert.InRange(head.LineCount, limit + 1, full.LineCount - 1);
        }
    }

    [Theory]
    [InlineData(false, 3, 5)]
    [InlineData(false, 12, 5)]
    [InlineData(true, 12, 5)]
    [InlineData(true, 12, 11)]
    [InlineData(true, 3, 3)]
    public void GivenOneLongList_WhenRenderingTheHead_ThenItStopsInsideTheListAndIsThePrefix(bool ordered, int items, int limit)
    {
        var lines = Enumerable.Range(1, items).Select(number => $"{(ordered ? $"{number}." : "-")} item {number} of the list");
        var document = AdfMarkdownParser.Parse(string.Join('\n', lines));
        var full = AdfRenderer.Render(document, Width);

        var (head, complete) = AdfRenderer.RenderHead(document, Width, limit);

        Assert.Equal(PlainOf(full.Lines.Take(head.LineCount)), PlainOf(head.Lines));

        if (complete)
        {
            Assert.Equal(full.LineCount, head.LineCount);
        }
        else
        {
            Assert.Equal(limit + 1, head.LineCount);
            Assert.True(full.LineCount > head.LineCount);
        }
    }

    [Fact]
    public void GivenACutComment_WhenReadingTheNote_ThenItCountsTheHiddenLinesOfTheWholeBody()
    {
        var document = Paragraphs(30);
        var expected = AdfRenderer.Render(document, Width).LineCount - CommentRows.BodyLines;
        var memo = new DetailRenderMemo();

        var rows = CommentRows.Build(DetailOf(document), Width, CommentRows.BodyLines, memo);

        Assert.Equal(6, rows.Count);
        Assert.Contains($"… {expected} more lines   za expand", rows[4].Line.PlainText, StringComparison.Ordinal);
        Assert.True(rows[4].Target!.Note);
        Assert.False(rows[4].IsStop);
    }

    [Fact]
    public void GivenALongComment_WhenBuildingRows_ThenOnlyTheHeadIsKeptInTheMemoUntilTheNoteIsRead()
    {
        var document = Paragraphs(30);
        var memo = new DetailRenderMemo();

        var rows = CommentRows.Build(DetailOf(document), Width, CommentRows.BodyLines, memo);

        Assert.Equal(6, rows.Count);
        Assert.Equal(0, memo.WholeRenderings);

        _ = rows[4].Line;

        Assert.Equal(1, memo.WholeRenderings);
    }

    [Fact]
    public void GivenAWideEnoughPane_WhenBuildingCutRows_ThenTheyEqualTheRowsOfAnUncutBuild()
    {
        const int width = Width;
        var document = Paragraphs(30);
        var detail = DetailOf(document);

        var lazy = CommentRows.Build(detail, width, CommentRows.BodyLines, new DetailRenderMemo());
        var eager = CommentRows.Build(detail with { Expanded = ["comment:1"] }, width);

        Assert.Equal(CommentRows.BodyLines + 3, lazy.Count);

        for (var row = 0; row < CommentRows.BodyLines + 1; row++)
        {
            Assert.Equal(eager[row].Line.PlainText, lazy[row].Line.PlainText);
            Assert.Equal(eager[row].Comment, lazy[row].Comment);
        }

        Assert.All(lazy.Skip(1).Take(CommentRows.BodyLines), row => Assert.False(row.IsStop));
        Assert.True(lazy[0].IsStop);
        Assert.True(lazy[0].Target!.Expands);
        Assert.Contains("more lines", lazy[^2].Line.PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAPaneTooNarrowForTheNote_WhenBuildingCutRows_ThenTheBodyIsRenderedWholeAsBefore()
    {
        var memo = new DetailRenderMemo();

        var rows = CommentRows.Build(DetailOf(Paragraphs(30)), 20, CommentRows.BodyLines, memo);

        Assert.Equal(1, memo.WholeRenderings);
        Assert.Equal(CommentRows.BodyLines + 1, rows.Count(row => row.Comment == 0 && row.Target is not null && !row.Target.Note) );
        Assert.Contains(rows, row => row.Target is { Note: true });
    }

    [Fact]
    public void GivenARowMadeOfASource_WhenReadingTwice_ThenTheSourceRunsOnce()
    {
        var memo = new DetailRenderMemo();
        var rows = CommentRows.Build(DetailOf(Paragraphs(30)), Width, CommentRows.BodyLines, memo);

        var first = rows[4].Line;

        Assert.Same(first, rows[4].Line);
        Assert.Equal(1, memo.WholeRenderings);
    }

    [Fact]
    public void GivenTheSameRows_WhenAskingForTheIdsTwice_ThenTheListIsNotCopiedAgain()
    {
        var memo = new DetailRenderMemo();
        var detail = DetailOf(Paragraphs(5)) with { Render = memo };
        var rows = memo.Rows(detail, Width, CommentRows.BodyLines);

        var first = memo.IdsOf(rows);
        var second = memo.IdsOf(rows);

        Assert.Same(first, second);
        Assert.Equal("1", first[0]);
        Assert.Null(first[^1]);
    }

    private static string[] PlainOf(IEnumerable<StyledLine> lines)
    {
        return [.. lines.Select(line => line.PlainText)];
    }

    private static AdfDocument Paragraphs(int count)
    {
        var texts = Enumerable.Range(1, count).Select(number => $"paragraph {number} with some words to wrap around");

        return AdfMarkdownParser.Parse(string.Join("\n\n", texts));
    }

    private static DetailState DetailOf(AdfDocument body)
    {
        var comment = new IssueComment("1", _eve, body, _now.AddHours(-2), _now.AddHours(-2));

        return DetailState.Opened("A-1", null, 1, 2, 3) with
        {
            Comments = [comment],
            LoadedAt = _now,
            CommentsLoad = LoadState.Idle,
            NextComments = null,
        };
    }
}
