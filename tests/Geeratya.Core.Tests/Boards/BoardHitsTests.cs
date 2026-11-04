using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardHitsTests
{
    private const int Width = 60;
    private const int VisibleRows = 30;

    [Theory]
    [InlineData(1, 0, CardRowKind.Top)]
    [InlineData(5, 0, CardRowKind.Blank)]
    [InlineData(6, 0, CardRowKind.Key)]
    [InlineData(7, 0, CardRowKind.Bottom)]
    [InlineData(8, 1, CardRowKind.Top)]
    [InlineData(11, 1, CardRowKind.Key)]
    public void GivenCardsOfDifferentHeight_WhenLocatingARow_ThenTheCardAndItsRowKindAreFound(
        int row,
        int card,
        CardRowKind kind)
    {
        var columns = Columns();
        var cursor = Cursor();

        var located = BoardHits.Locate(columns, cursor, VisibleRows, Width, 5, row);

        Assert.Equal(card, located?.Hit.Card);
        Assert.Equal(kind, located?.Row.Kind);
    }

    [Fact]
    public void GivenARowUnderTheLastCard_WhenLocating_ThenThereIsNoHit()
    {
        var columns = Columns();
        var cursor = Cursor();

        Assert.Null(BoardHits.Locate(columns, cursor, VisibleRows, Width, 5, 14));
        Assert.Null(BoardHits.At(columns, cursor, VisibleRows, Width, 5, 14));
    }

    [Fact]
    public void GivenALabelledCard_WhenHittingItsBlankRow_ThenTheCardIsHitNotTheNextOne()
    {
        var columns = Columns();
        var cursor = Cursor();

        var hit = BoardHits.At(columns, cursor, VisibleRows, Width, 5, 7);

        Assert.Equal(0, hit?.Card);
    }

    [Fact]
    public void GivenACompactBoard_WhenLocating_ThenEveryRowIsAOneLineCardWithAKeyRow()
    {
        var columns = Columns();
        var cursor = Cursor();

        var located = BoardHits.Locate(columns, cursor, 4, Width, 5, 2);

        Assert.Equal(1, located?.Hit.Card);
        Assert.Equal(CardRowKind.Key, located?.Row.Kind);
    }

    private static List<BoardColumnGroup> Columns()
    {
        return [new BoardColumnGroup("To Do", [Issue("A-1", ["x"]), Issue("A-2", [])])];
    }

    private static BoardCursor Cursor()
    {
        return new BoardCursor(0, [0], [0], 0);
    }

    private static IssueSummary Issue(string key, IReadOnlyList<string> labels)
    {
        return new IssueSummary(
            key,
            key,
            "Short",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            labels,
            null,
            null,
            "A");
    }
}
