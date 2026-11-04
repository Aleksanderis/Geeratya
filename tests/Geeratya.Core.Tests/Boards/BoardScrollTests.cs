using Geeratya.Core.Boards;
using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardScrollTests
{
    private const int Count = 40;

    // Card area of an 80x24 and a 120x36 terminal on a four-column board.
    private static readonly BoardViewport _small = new(18, 80);
    private static readonly BoardViewport _large = new(30, 120);

    [Fact]
    public void GivenShortCards_WhenCountingWhatFits_ThenTheRealHeightIsUsedNotTheTallest()
    {
        var group = Column(Count, _ => IssueWith("Short", []));

        var fitting = BoardScroll.Fitting(group, 0, _large, CellWidth(_large));

        Assert.Equal(_large.CardRows / 5, fitting);
        Assert.True(fitting > BoardCards.Visible(_large.VisibleRows, ListLayout.HeaderRows));
    }

    [Theory]
    [InlineData(18, 80)]
    [InlineData(30, 120)]
    public void GivenMixedHeights_WhenMovingDownTheColumn_ThenTheContextBelowTheCursorStaysDrawn(
        int visibleRows,
        int width)
    {
        var viewport = new BoardViewport(visibleRows, width);
        var group = Mixed();
        var cell = CellWidth(viewport);
        var position = new ListPosition(0, 0);

        for (var step = 0; step < Count - 1; step++)
        {
            position = BoardScroll.Move(group, position, ListMove.Down, viewport, cell);

            var placed = BoardColumnLayout.Place(group, position.Scroll, cell, viewport.CardRows);
            var wanted = Math.Min(Count - 1, position.Selected + ExpectedContext(group, position.Selected, viewport, cell));

            Assert.Equal(step + 1, position.Selected);
            Assert.Contains(placed, card => card.Card == wanted);
        }
    }

    [Theory]
    [InlineData(18, 80)]
    [InlineData(30, 120)]
    public void GivenMixedHeights_WhenMovingUpFromTheBottom_ThenTheCursorAndItsContextAboveStayDrawn(
        int visibleRows,
        int width)
    {
        var viewport = new BoardViewport(visibleRows, width);
        var group = Mixed();
        var cell = CellWidth(viewport);
        var position = BoardScroll.Move(group, new ListPosition(0, 0), ListMove.Bottom, viewport, cell);

        for (var step = Count - 1; step > 0; step--)
        {
            position = BoardScroll.Move(group, position, ListMove.Up, viewport, cell);

            var placed = BoardColumnLayout.Place(group, position.Scroll, cell, viewport.CardRows);
            var wanted = Math.Max(0, position.Selected - ExpectedContext(group, position.Selected, viewport, cell));

            Assert.Equal(step - 1, position.Selected);
            Assert.Contains(placed, card => card.Card == position.Selected);
            Assert.True(position.Scroll <= wanted);
        }
    }

    [Fact]
    public void GivenTheLastCard_WhenRevealing_ThenNoBlankRowsStayBelowWhileEarlierCardsCouldFillThem()
    {
        var group = Mixed();
        var cell = CellWidth(_large);

        var bottom = BoardScroll.Reveal(group, new ListPosition(Count - 1, 0), _large, cell);
        var placed = BoardColumnLayout.Place(group, bottom.Scroll, cell, _large.CardRows);
        var one = BoardColumnLayout.Place(group, bottom.Scroll - 1, cell, _large.CardRows);

        Assert.Equal(Count - 1, placed[^1].Card);
        Assert.True(bottom.Scroll > 0);
        Assert.True(one[^1].Card < Count - 1);
    }

    [Fact]
    public void GivenAHalfPageMove_WhenPaging_ThenItMovesByHalfTheCardsThatReallyFit()
    {
        var group = Column(Count, _ => IssueWith("Short", []));
        var cell = CellWidth(_large);
        var half = BoardScroll.Fitting(group, 5, _large, cell) / 2;

        var down = BoardScroll.Move(group, new ListPosition(10, 5), ListMove.PageDown, _large, cell);
        var up = BoardScroll.Move(group, new ListPosition(10, 5), ListMove.PageUp, _large, cell);

        Assert.Equal(3, half);
        Assert.Equal(10 + half, down.Selected);
        Assert.Equal(10 - half, up.Selected);
    }

    [Fact]
    public void GivenAPageMoveNearTheEnds_WhenPaging_ThenTheCursorStopsAtTheFirstAndLastCard()
    {
        var group = Mixed();
        var cell = CellWidth(_small);

        var down = BoardScroll.Move(group, new ListPosition(Count - 2, 30), ListMove.PageDown, _small, cell);
        var up = BoardScroll.Move(group, new ListPosition(1, 0), ListMove.PageUp, _small, cell);

        Assert.Equal(Count - 1, down.Selected);
        Assert.Equal(0, up.Selected);
    }

    [Fact]
    public void GivenAnUnknownWidth_WhenMeasuring_ThenEveryFullCardCountsTheTallest()
    {
        var group = Column(Count, _ => IssueWith("Short", []));
        var viewport = new BoardViewport(30, 0);

        Assert.Equal(BoardCardPlan.MaxRows, BoardScroll.Height(group, 0, viewport, 0));
        Assert.Equal(viewport.CardRows / BoardCardPlan.MaxRows, BoardScroll.Fitting(group, 0, viewport, 0));
    }

    [Fact]
    public void GivenCompactCards_WhenMeasuring_ThenEveryCardIsOneRow()
    {
        var group = Mixed();
        var viewport = new BoardViewport(5, 120);
        var cell = CellWidth(viewport);

        Assert.True(viewport.Compact);
        Assert.Equal(1, BoardScroll.Height(group, 3, viewport, cell));
        Assert.Equal(viewport.CardRows, BoardScroll.Fitting(group, 0, viewport, cell));
    }

    [Fact]
    public void GivenAnEmptyColumnOrNoRoom_WhenMovingAndRevealing_ThenNothingThrows()
    {
        var empty = Column(0, _ => IssueWith("x", []));
        var one = Column(1, _ => IssueWith("x", []));
        var none = new BoardViewport(-5, 120);

        var moved = BoardScroll.Move(empty, new ListPosition(3, 3), ListMove.PageDown, _large, 20);
        var revealed = BoardScroll.Reveal(one, new ListPosition(0, 4), none, 20);

        Assert.Equal(new ListPosition(0, 0), moved);
        Assert.Equal(new ListPosition(0, 0), revealed);
    }

    [Fact]
    public void GivenAnyCard_WhenAskingTheHeight_ThenItEqualsTheRowsTheRendererPlaces()
    {
        var group = Mixed();
        var cell = CellWidth(_large);

        for (var card = 0; card < Count; card++)
        {
            var part = BoardColumnLayout.PartOf(group, card);

            Assert.Equal(BoardCardPlan.Rows(group.Rows[card], part, cell).Count, BoardScroll.Height(group, card, _large, cell));
        }
    }

    private static int CellWidth(BoardViewport viewport)
    {
        return BoardWindows.For(4, 0, viewport.Width).CellWidth(0);
    }

    private static int ExpectedContext(BoardColumnGroup group, int selected, BoardViewport viewport, int cell)
    {
        for (var context = ListMotion.ScrollOff; context > 0; context--)
        {
            var rows = 0;

            for (var card = Math.Max(0, selected - context); card <= Math.Min(Count - 1, selected + context); card++)
            {
                rows += BoardScroll.Height(group, card, viewport, cell);
            }

            if (rows <= viewport.CardRows)
            {
                return context;
            }
        }

        return 0;
    }

    private static BoardColumnGroup Mixed()
    {
        return Column(Count, index => (index % 3) switch
        {
            0 => IssueWith("Short", []),
            1 => IssueWith("A summary long enough to wrap onto a second line of the card", ["alpha"]),
            _ => IssueWith("Mid", ["alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta", "theta"]),
        });
    }

    private static BoardColumnGroup Column(int size, Func<int, IssueSummary> issue)
    {
        return new BoardColumnGroup("Column", [.. Enumerable.Range(0, size).Select(issue)]);
    }

    private static IssueSummary IssueWith(string summary, string[] labels)
    {
        return new IssueSummary(
            "K-1",
            "K-1",
            summary,
            new IssueStatus("1", "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            labels,
            null,
            null,
            "C");
    }
}
