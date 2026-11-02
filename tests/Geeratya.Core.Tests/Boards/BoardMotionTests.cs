using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardMotionTests
{
    private const int Visible = 3;

    [Fact]
    public void GivenNoCursor_WhenResolving_ThenTheColumnOfTheSelectedKeyIsSelected()
    {
        var columns = Columns(2, 0, 3);

        var cursor = BoardMotion.Resolve(columns, null, "C2-1", Visible);

        Assert.Equal(2, cursor.Column);
        Assert.Equal(1, cursor.Card);
    }

    [Fact]
    public void GivenNoCursorAndNoKey_WhenResolving_ThenTheFirstColumnIsSelected()
    {
        var cursor = BoardMotion.Resolve(Columns(2, 2), null, null, Visible);

        Assert.Equal(0, cursor.Column);
        Assert.Equal(0, cursor.Card);
    }

    [Fact]
    public void GivenNoColumns_WhenMoving_ThenNothingThrowsAndTheCursorIsEmpty()
    {
        var left = BoardMotion.MoveColumn([], null, null, BoardColumnMove.Left, Visible);
        var down = BoardMotion.MoveCard([], null, null, ListMove.Down, Visible);

        Assert.Empty(left.Cards);
        Assert.Empty(down.Scrolls);
        Assert.Null(BoardMotion.SelectedIssue([], down));
    }

    [Fact]
    public void GivenCardInTallColumn_WhenMovingToAShorterColumn_ThenTheRowIsClampedToItsLastCard()
    {
        var columns = Columns(2, 5, 1);
        var third = BoardMotion.Resolve(columns, null, "C1-3", Visible);
        var onFifth = BoardMotion.MoveCard(columns, third, "C1-3", ListMove.Down, Visible);

        var left = BoardMotion.MoveColumn(columns, onFifth, "C1-4", BoardColumnMove.Left, Visible);
        var right = BoardMotion.MoveColumn(columns, onFifth, "C1-4", BoardColumnMove.Right, Visible);

        Assert.Equal(0, left.Column);
        Assert.Equal(1, left.Card);
        Assert.Equal(2, right.Column);
        Assert.Equal(0, right.Card);
        Assert.Equal("C2-0", BoardMotion.SelectedIssue(columns, right)?.Key);
    }

    [Fact]
    public void GivenEqualHeights_WhenMovingColumns_ThenTheRowIndexIsKept()
    {
        var columns = Columns(4, 4, 4);
        var cursor = BoardMotion.MoveCard(columns, null, null, ListMove.Down, Visible);

        var right = BoardMotion.MoveColumn(columns, cursor, "C0-1", BoardColumnMove.Right, Visible);

        Assert.Equal(1, right.Column);
        Assert.Equal(1, right.Card);
    }

    [Fact]
    public void GivenEmptyColumn_WhenMovingIntoIt_ThenItIsSelectableHasNoIssueAndRowKeysDoNothing()
    {
        var columns = Columns(2, 0, 2);
        var cursor = BoardMotion.MoveColumn(columns, null, null, BoardColumnMove.Right, Visible);

        var down = BoardMotion.MoveCard(columns, cursor, "C0-0", ListMove.Down, Visible);
        var bottom = BoardMotion.MoveCard(columns, cursor, "C0-0", ListMove.Bottom, Visible);

        Assert.Equal(1, cursor.Column);
        Assert.Null(BoardMotion.SelectedIssue(columns, cursor));
        Assert.Equal(1, down.Column);
        Assert.Equal(0, down.Card);
        Assert.Equal(0, bottom.Card);
    }

    [Fact]
    public void GivenRowFourChosen_WhenCrossingAShortAndAnEmptyColumn_ThenTheRowComesBackInTheTallOne()
    {
        var columns = Columns(8, 2, 0, 8);
        var chosen = BoardMotion.MoveCard(columns, null, null, ListMove.Bottom, Visible);
        var four = BoardMotion.MoveCard(columns, chosen, "C0-7", ListMove.Top, Visible);

        for (var step = 0; step < 4; step++)
        {
            four = BoardMotion.MoveCard(columns, four, null, ListMove.Down, Visible);
        }

        var cursor = four;

        foreach (var _ in Enumerable.Range(0, 3))
        {
            cursor = BoardMotion.MoveColumn(columns, cursor, null, BoardColumnMove.Right, Visible);
        }

        Assert.Equal(3, cursor.Column);
        Assert.Equal(4, cursor.Card);
        Assert.Equal(1, BoardMotion.MoveColumn(columns, four, null, BoardColumnMove.Right, Visible).Card);
    }

    [Fact]
    public void GivenEmptyColumnSelectedAfterMoving_WhenResolvingWithStaleKey_ThenTheChosenColumnDoesNotJumpBack()
    {
        var columns = Columns(2, 0, 2);
        var cursor = BoardMotion.MoveColumn(columns, null, null, BoardColumnMove.Right, Visible);

        var resolved = BoardMotion.Resolve(columns, cursor, "C0-0", Visible);

        Assert.Equal(1, resolved.Column);
    }

    [Theory]
    [InlineData(BoardColumnMove.First, 0)]
    [InlineData(BoardColumnMove.Last, 3)]
    [InlineData(BoardColumnMove.Left, 0)]
    public void GivenFirstColumnSelected_WhenMovingAcrossTheBoard_ThenItStopsAtTheEnds(BoardColumnMove move, int expected)
    {
        var columns = Columns(1, 1, 1, 1);

        var cursor = BoardMotion.MoveColumn(columns, null, null, move, Visible);

        Assert.Equal(expected, cursor.Column);
    }

    [Fact]
    public void GivenLastColumnSelected_WhenMovingRight_ThenItStaysThere()
    {
        var columns = Columns(1, 1);
        var last = BoardMotion.MoveColumn(columns, null, null, BoardColumnMove.Last, Visible);

        var again = BoardMotion.MoveColumn(columns, last, null, BoardColumnMove.Right, Visible);

        Assert.Equal(1, again.Column);
    }

    [Fact]
    public void GivenLongColumn_WhenPagingAndJumping_ThenSelectionAndScrollFollow()
    {
        var columns = Columns(20);
        var cursor = BoardMotion.MoveCard(columns, null, null, ListMove.Bottom, Visible);

        Assert.Equal(19, cursor.Card);
        Assert.Equal(17, cursor.Scrolls[0]);

        var top = BoardMotion.MoveCard(columns, cursor, "C0-19", ListMove.Top, Visible);
        var page = BoardMotion.MoveCard(columns, top, "C0-0", ListMove.PageDown, 10);

        Assert.Equal(0, top.Card);
        Assert.Equal(0, top.Scrolls[0]);
        Assert.Equal(5, page.Card);
    }

    [Fact]
    public void GivenTwoScrolledColumns_WhenScrollingTheSecond_ThenTheFirstKeepsItsOwnScroll()
    {
        var columns = Columns(20, 20);
        var bottom = BoardMotion.MoveCard(columns, null, null, ListMove.Bottom, Visible);

        var right = BoardMotion.MoveColumn(columns, bottom, "C0-19", BoardColumnMove.Right, Visible);
        var top = BoardMotion.MoveCard(columns, right, "C1-19", ListMove.Top, Visible);

        Assert.Equal(17, bottom.Scrolls[0]);
        Assert.Equal(0, top.Scrolls[1]);
        Assert.Equal(17, top.Scrolls[0]);
        Assert.Equal(19, top.Cards[0]);
    }

    [Fact]
    public void GivenCursorAfterRefresh_WhenTheKeyMovedInsideItsColumn_ThenTheCardFollowsTheKey()
    {
        var before = Columns(3, 3);
        var cursor = BoardMotion.MoveCard(before, null, null, ListMove.Down, Visible);
        var after = new List<BoardColumnGroup>
        {
            new("Column 0", [Issue("C0-9", 0), Issue("C0-8", 0), Issue("C0-0", 0), Issue("C0-1", 0)]),
            before[1],
        };

        var resolved = BoardMotion.Resolve(after, cursor, "C0-1", Visible);

        Assert.Equal(3, resolved.Card);
    }

    [Fact]
    public void GivenCursorAfterRefresh_WhenColumnsAreFewer_ThenTheCursorIsClamped()
    {
        var wide = Columns(1, 1, 1);
        var last = BoardMotion.MoveColumn(wide, null, null, BoardColumnMove.Last, Visible);

        var resolved = BoardMotion.Resolve(Columns(1, 1), last, "C2-0", Visible);

        Assert.Equal(1, resolved.Column);
        Assert.Equal(2, resolved.Cards.Count);
    }

    [Fact]
    public void GivenZeroViewport_WhenMoving_ThenNothingThrows()
    {
        var columns = Columns(5, 5);

        var down = BoardMotion.MoveCard(columns, null, null, ListMove.Down, 0);
        var right = BoardMotion.MoveColumn(columns, down, "C0-1", BoardColumnMove.Right, 0);

        Assert.Equal(1, right.Column);
        Assert.Equal(1, right.Card);
        Assert.Equal(0, right.Scrolls[1]);
    }

    private static List<BoardColumnGroup> Columns(params int[] sizes)
    {
        return [.. sizes.Select((size, column) => new BoardColumnGroup(
            $"Column {column}",
            [.. Enumerable.Range(0, size).Select(card => Issue($"C{column}-{card}", column))]))];
    }

    private static IssueSummary Issue(string key, int column)
    {
        return new IssueSummary(
            key,
            key,
            $"Summary {key}",
            new IssueStatus(column.ToString(System.Globalization.CultureInfo.InvariantCulture), "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "C");
    }
}
