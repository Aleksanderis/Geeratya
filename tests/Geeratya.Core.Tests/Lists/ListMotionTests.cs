using Geeratya.Core.Lists;

namespace Geeratya.Core.Tests.Lists;

[Trait("Os", "Independent")]
public class ListMotionTests
{
    [Theory]
    [InlineData(ListMove.Down)]
    [InlineData(ListMove.Up)]
    [InlineData(ListMove.Top)]
    [InlineData(ListMove.Bottom)]
    [InlineData(ListMove.PageDown)]
    [InlineData(ListMove.PageUp)]
    public void GivenAnEmptyList_WhenMoving_ThenPositionStaysAtZero(ListMove move)
    {
        var result = ListMotion.Move(new ListPosition(5, 5), move, 1, 0, 10);

        Assert.Equal(new ListPosition(0, 0), result);
    }

    [Fact]
    public void GivenOneRow_WhenMovingAnywhere_ThenTheSelectionStaysOnIt()
    {
        foreach (var move in Enum.GetValues<ListMove>())
        {
            Assert.Equal(new ListPosition(0, 0), ListMotion.Move(new ListPosition(0, 0), move, 3, 1, 10));
        }
    }

    [Fact]
    public void GivenAtTheEnds_WhenMovingPast_ThenTheSelectionIsClamped()
    {
        Assert.Equal(0, ListMotion.Move(new ListPosition(0, 0), ListMove.Up, 1, 20, 10).Selected);
        Assert.Equal(19, ListMotion.Move(new ListPosition(19, 10), ListMove.Down, 1, 20, 10).Selected);
        Assert.Equal(19, ListMotion.Move(new ListPosition(18, 10), ListMove.PageDown, 1, 20, 10).Selected);
        Assert.Equal(0, ListMotion.Move(new ListPosition(2, 0), ListMove.PageUp, 1, 20, 10).Selected);
    }

    [Fact]
    public void GivenCount_WhenMovingDown_ThenItMovesThatManyRows()
    {
        Assert.Equal(7, ListMotion.Move(new ListPosition(2, 0), ListMove.Down, 5, 50, 20).Selected);
        Assert.Equal(3, ListMotion.Move(new ListPosition(2, 0), ListMove.Down, 0, 50, 20).Selected);
        Assert.Equal(49, ListMotion.Move(new ListPosition(2, 0), ListMove.Down, int.MaxValue, 50, 20).Selected);
    }

    [Fact]
    public void GivenAPage_WhenPaging_ThenItMovesHalfTheViewportAndAtLeastOneRow()
    {
        Assert.Equal(15, ListMotion.Move(new ListPosition(5, 0), ListMove.PageDown, 1, 100, 20).Selected);
        Assert.Equal(5, ListMotion.Move(new ListPosition(15, 0), ListMove.PageUp, 1, 100, 20).Selected);
        Assert.Equal(6, ListMotion.Move(new ListPosition(5, 5), ListMove.PageDown, 1, 100, 1).Selected);
        Assert.Equal(6, ListMotion.Move(new ListPosition(5, 5), ListMove.PageDown, 1, 100, 0).Selected);
    }

    [Fact]
    public void GivenTopAndBottom_WhenJumping_ThenScrollFollows()
    {
        var bottom = ListMotion.Move(new ListPosition(0, 0), ListMove.Bottom, 1, 100, 10);
        var top = ListMotion.Move(bottom, ListMove.Top, 1, 100, 10);

        Assert.Equal(new ListPosition(99, 90), bottom);
        Assert.Equal(new ListPosition(0, 0), top);
    }

    [Fact]
    public void GivenSelectionNearTheEdges_WhenScrolling_ThenThreeRowsOfContextStay()
    {
        var down = new ListPosition(0, 0);

        for (var step = 0; step < 8; step++)
        {
            down = ListMotion.Move(down, ListMove.Down, 1, 100, 10);
        }

        Assert.Equal(new ListPosition(8, 2), down);

        var up = down;

        for (var step = 0; step < 3; step++)
        {
            up = ListMotion.Move(up, ListMove.Up, 1, 100, 10);
        }

        Assert.Equal(new ListPosition(5, 2), up);
        Assert.Equal(new ListPosition(4, 1), ListMotion.Move(up, ListMove.Up, 1, 100, 10));
    }

    [Fact]
    public void GivenFewRows_WhenRevealing_ThenTheScrollNeverLeavesBlankRows()
    {
        var result = ListMotion.Reveal(new ListPosition(4, 3), 5, 10);

        Assert.Equal(new ListPosition(4, 0), result);
    }

    [Fact]
    public void GivenAVeryShortViewport_WhenScrolling_ThenContextShrinksAndTheSelectionStaysVisible()
    {
        for (var viewport = 1; viewport <= 8; viewport++)
        {
            var position = new ListPosition(0, 0);

            for (var step = 0; step < 30; step++)
            {
                position = ListMotion.Move(position, ListMove.Down, 1, 30, viewport);

                Assert.InRange(position.Selected, position.Scroll, position.Scroll + viewport - 1);
            }
        }
    }

    [Fact]
    public void GivenAZeroViewportOrStaleScroll_WhenRevealing_ThenNothingThrows()
    {
        Assert.Equal(new ListPosition(3, 0), ListMotion.Reveal(new ListPosition(3, 50), 10, 0));
        Assert.Equal(new ListPosition(0, 0), ListMotion.Reveal(new ListPosition(-4, -9), 10, 5));
        Assert.Equal(new ListPosition(9, 5), ListMotion.Reveal(new ListPosition(99, 99), 10, 5));
    }
}
