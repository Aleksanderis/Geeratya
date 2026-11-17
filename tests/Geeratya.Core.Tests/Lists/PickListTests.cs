using Geeratya.Core.Lists;

namespace Geeratya.Core.Tests.Lists;

public class PickListTests
{
    [Fact]
    public void GivenAFreshList_WhenReading_ThenTheFirstRowIsHighlightedAndNothingIsPending()
    {
        var list = PickList.First;

        Assert.Equal(0, list.Selected);
        Assert.Equal(PickStatus.Ready, list.Status);
        Assert.Null(list.Message);
    }

    [Theory]
    [InlineData(0, 1, 5, 1)]
    [InlineData(4, 1, 5, 4)]
    [InlineData(0, -1, 5, 0)]
    [InlineData(2, 100, 5, 4)]
    [InlineData(2, -100, 5, 0)]
    [InlineData(0, 3, 0, 0)]
    public void GivenARowCount_WhenMoving_ThenTheHighlightStopsAtTheEnds(int start, int delta, int count, int expected)
    {
        Assert.Equal(expected, new PickList(start).Moved(delta, count).Selected);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(-1)]
    public void GivenNoHighlight_WhenMovingEitherWay_ThenTheFirstRowIsHighlighted(int delta)
    {
        Assert.Equal(0, PickList.Unhighlighted.Moved(delta, 4).Selected);
    }

    [Fact]
    public void GivenNoHighlight_WhenAskingForTheIndex_ThenItStaysNoHighlight()
    {
        Assert.Equal(-1, PickList.Unhighlighted.Index(4));
    }

    [Theory]
    [InlineData(9, 3, 2)]
    [InlineData(1, 3, 1)]
    [InlineData(5, 0, 0)]
    public void GivenRowsThatShrank_WhenReadingTheIndex_ThenItIsClamped(int selected, int count, int expected)
    {
        Assert.Equal(expected, new PickList(selected).Index(count));
    }

    [Theory]
    [InlineData(ListMove.PageDown, 1, 4, 20, 10, 9)]
    [InlineData(ListMove.PageUp, 1, 14, 20, 10, 9)]
    [InlineData(ListMove.Top, 1, 14, 20, 10, 0)]
    [InlineData(ListMove.Bottom, 1, 3, 20, 10, 19)]
    [InlineData(ListMove.Down, 3, 0, 20, 10, 3)]
    public void GivenAListMove_WhenMoving_ThenItFollowsTheSharedCursorRules(
        ListMove move,
        int times,
        int start,
        int count,
        int viewport,
        int expected)
    {
        Assert.Equal(expected, new PickList(start).Moved(move, times, count, viewport).Selected);
    }

    [Fact]
    public void GivenALongList_WhenWindowing_ThenTheHighlightStaysInViewWithTheSharedMargin()
    {
        var window = new PickList(10).Window(30, 5);

        Assert.Contains(10, window.Indexes);
        Assert.Equal(5, window.Shown);
        Assert.Equal(ListCursor.FirstVisible(10, 30, 5), window.First);
    }

    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(3, 0, 0)]
    public void GivenAnEmptyListOrNoRoom_WhenWindowing_ThenNothingIsShown(int count, int area, int selected)
    {
        var window = new PickList(selected).Window(count, area);

        Assert.Equal(0, window.Shown);
        Assert.Equal(0, window.First);
    }

    [Fact]
    public void GivenAShortList_WhenWindowing_ThenAllRowsShowFromTheTop()
    {
        var window = new PickList(2).Window(3, 5);

        Assert.Equal([0, 1, 2], window.Indexes);
    }

    [Fact]
    public void GivenALookup_WhenItGoesThroughItsStates_ThenTheHighlightStaysAndTheMessageFollows()
    {
        var list = new PickList(2).Waiting();

        Assert.Equal(PickStatus.Waiting, list.Status);

        list = list.Loading();

        Assert.Equal(PickStatus.Loading, list.Status);

        list = list.Failed("boom");

        Assert.Equal(PickStatus.Failed, list.Status);
        Assert.Equal("boom", list.Message);

        list = list.Ready();

        Assert.Equal(PickStatus.Ready, list.Status);
        Assert.Null(list.Message);
        Assert.Equal(2, list.Selected);
    }

    [Fact]
    public void GivenAHighlightPastTheRows_WhenPlacingIt_ThenItIsClamped()
    {
        Assert.Equal(3, new PickList(0).At(99, 4).Selected);
        Assert.Equal(0, new PickList(0).At(-5, 4).Selected);
    }
}
