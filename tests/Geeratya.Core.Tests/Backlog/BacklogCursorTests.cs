using Geeratya.Core.Backlog;
using Geeratya.Core.Lists;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Backlog;

[Trait("Os", "Independent")]
public class BacklogCursorTests
{
    private static readonly Sprint[] _sprints =
        [BacklogFixtures.Active, BacklogFixtures.Future, BacklogFixtures.EmptyFuture];

    [Fact]
    public void GivenTheCursorAtASectionEnd_WhenMovingDown_ThenItCrossesToTheNextSectionWithRows()
    {
        var tab = Tab() with { Selected = 3, SelectedKey = "A-6" };

        var moved = BacklogCursor.Move(tab, ListMove.Down);

        Assert.Equal(4, moved.Selected);
        Assert.Equal("A-1", moved.SelectedKey);
    }

    [Fact]
    public void GivenACollapsedSection_WhenMovingDownThroughIt_ThenTheCursorStopsOnlyOnItsFirstRow()
    {
        var tab = Tab(collapsed: [BacklogSections.SprintKey(BacklogFixtures.FutureId)]) with
        {
            Selected = 1,
            SelectedKey = "A-4",
        };

        var onHeader = BacklogCursor.Move(tab, ListMove.Down);
        var past = BacklogCursor.Move(onHeader, ListMove.Down);

        Assert.Equal(2, onHeader.Selected);
        Assert.Equal(4, past.Selected);
    }

    [Fact]
    public void GivenACollapsedSection_WhenMovingUpFromBelow_ThenTheCursorLandsOnItsHeaderRow()
    {
        var tab = Tab(collapsed: [BacklogSections.SprintKey(BacklogFixtures.FutureId)]) with
        {
            Selected = 4,
            SelectedKey = "A-1",
        };

        var moved = BacklogCursor.Move(tab, ListMove.Up);

        Assert.Equal(2, moved.Selected);
    }

    [Fact]
    public void GivenTopAndBottom_WhenMoving_ThenTheFirstAndLastStopAreReached()
    {
        var tab = Tab() with { Selected = 2, SelectedKey = "A-2" };

        Assert.Equal(0, BacklogCursor.Move(tab, ListMove.Top).Selected);
        Assert.Equal(5, BacklogCursor.Move(tab, ListMove.Bottom).Selected);
    }

    [Fact]
    public void GivenTheLastRowSelected_WhenMovingDown_ThenNothingMoves()
    {
        var tab = Tab() with { Selected = 5, SelectedKey = "A-5" };

        Assert.Equal(5, BacklogCursor.Move(tab, ListMove.Down).Selected);
    }

    [Fact]
    public void GivenAnEmptyBacklog_WhenMovingOrToggling_ThenNothingThrows()
    {
        var tab = BacklogFixtures.Tab([], _sprints);

        Assert.Same(tab, BacklogCursor.Move(tab, ListMove.Down));
        Assert.Same(tab, BacklogCursor.Toggle(tab));
        Assert.Same(tab, BacklogCursor.Jump(tab, 1));
        Assert.Equal(0, BacklogCursor.Settle(tab, 10).Scroll);
    }

    [Fact]
    public void GivenARowInsideASection_WhenTheSectionIsCollapsed_ThenTheCursorMovesToItsFirstRow()
    {
        var tab = Tab() with { Selected = 1, SelectedKey = "A-4" };

        var collapsed = BacklogCursor.Toggle(tab);

        Assert.Equal([BacklogSections.SprintKey(BacklogFixtures.ActiveId)], collapsed.CollapsedSections);
        Assert.Equal(0, collapsed.Selected);
        Assert.Equal("A-3", collapsed.SelectedKey);
        Assert.True(BacklogCursor.OnCollapsedHeader(collapsed));
    }

    [Fact]
    public void GivenACollapsedSection_WhenToggledAgain_ThenItExpandsAndTheCursorStays()
    {
        var collapsed = BacklogCursor.Toggle(Tab() with { Selected = 2, SelectedKey = "A-2" });

        var expanded = BacklogCursor.Toggle(collapsed);

        Assert.Empty(expanded.CollapsedSections);
        Assert.Equal(2, expanded.Selected);
        Assert.False(BacklogCursor.OnCollapsedHeader(expanded));
    }

    [Fact]
    public void GivenTheCursorInTheFirstSection_WhenJumpingToTheNextSection_ThenItLandsOnItsFirstRow()
    {
        var tab = Tab() with { Selected = 1, SelectedKey = "A-4" };

        var next = BacklogCursor.Jump(tab, 1);
        var afterNext = BacklogCursor.Jump(next, 1);
        var end = BacklogCursor.Jump(afterNext, 1);

        Assert.Equal(2, next.Selected);
        Assert.Equal(4, afterNext.Selected);
        Assert.Equal(4, end.Selected);
    }

    [Fact]
    public void GivenTheCursorInsideASection_WhenJumpingBack_ThenItGoesToTheStartOfThisSectionThenThePreviousOne()
    {
        var tab = Tab() with { Selected = 3, SelectedKey = "A-6" };

        var toStart = BacklogCursor.Jump(tab, -1);
        var toPrevious = BacklogCursor.Jump(toStart, -1);
        var atTop = BacklogCursor.Jump(toPrevious, -1);

        Assert.Equal(2, toStart.Selected);
        Assert.Equal(0, toPrevious.Selected);
        Assert.Equal(0, atTop.Selected);
    }

    [Fact]
    public void GivenAnEmptySectionBetweenOthers_WhenJumping_ThenItIsSkipped()
    {
        var tab = Tab() with { Selected = 2, SelectedKey = "A-2" };

        Assert.Equal(4, BacklogCursor.Jump(tab, 1).Selected);
    }

    [Fact]
    public void GivenAShortViewport_WhenMovingToTheLastRow_ThenTheWindowScrollsInLines()
    {
        var tab = Tab(viewport: 4) with { Selected = 0, SelectedKey = "A-3" };

        var moved = BacklogCursor.Move(tab, ListMove.Bottom);

        Assert.Equal(5, moved.Selected);
        Assert.Equal(8, moved.Scroll);
    }

    [Fact]
    public void GivenTheFirstRowOfASection_WhenMovingUpToIt_ThenItsHeaderComesIntoView()
    {
        var tab = Tab(viewport: 2) with { Selected = 5, SelectedKey = "A-5", Scroll = 10 };

        var moved = BacklogCursor.Place(tab, 4);

        Assert.Equal(4, moved.Selected);
        Assert.Equal(9, moved.Scroll);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void GivenTinyViewports_WhenSettled_ThenTheSelectionStaysInRange(int viewport)
    {
        var tab = Tab() with { Selected = 5, SelectedKey = "A-5" };

        var settled = BacklogCursor.Settle(tab, viewport);

        Assert.Equal(5, settled.Selected);
        Assert.InRange(settled.Scroll, 0, 12);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(4, 2)]
    [InlineData(8, null)]
    [InlineData(99, null)]
    public void GivenALine_WhenClicked_ThenRowLinesPickTheirRowHeadersTheSectionStartAndNotesNothing(int line, int? row)
    {
        var layout = BacklogSections.Build(Tab());

        Assert.Equal(row, BacklogCursor.RowAtLine(layout, line));
    }

    private static TabState Tab(IReadOnlyList<string>? collapsed = null, int viewport = 20)
    {
        var rows = BacklogSections.Order(BacklogFixtures.RankedRows);

        return BacklogFixtures.Tab(rows, _sprints, viewport) with { CollapsedSections = collapsed ?? [] };
    }
}
