using Geeratya.Core.Lists;

namespace Geeratya.Core.Tests.Lists;

[Trait("Os", "Independent")]
public class ListCursorTests
{
    [Theory]
    [InlineData(-5, 4, 0)]
    [InlineData(2, 4, 2)]
    [InlineData(9, 4, 3)]
    [InlineData(3, 0, 0)]
    public void GivenAnIndex_WhenClamping_ThenItStaysInsideTheList(int index, int count, int expected)
    {
        Assert.Equal(expected, ListCursor.Clamp(index, count));
    }

    [Theory]
    [InlineData(1, 3, 10, 4)]
    [InlineData(1, -3, 10, 0)]
    [InlineData(0, int.MaxValue, 10, 9)]
    [InlineData(0, int.MinValue, 10, 0)]
    [InlineData(0, 3, 0, 0)]
    public void GivenADelta_WhenStepping_ThenTheIndexIsClampedAndNeverOverflows(
        int index,
        int delta,
        int count,
        int expected)
    {
        Assert.Equal(expected, ListCursor.Step(index, delta, count));
    }

    [Theory]
    [InlineData(10, 5)]
    [InlineData(1, 1)]
    [InlineData(0, 1)]
    public void GivenAViewport_WhenTakingAHalfPage_ThenItIsHalfAndAtLeastOne(int viewport, int expected)
    {
        Assert.Equal(expected, ListCursor.Half(viewport));
    }

    [Fact]
    public void GivenAMove_WhenFindingTheTarget_ThenTopAndBottomIgnoreTheRepeatAndPagesUseTheHalf()
    {
        Assert.Equal(0, ListCursor.Target(7, ListMove.Top, 5, 20, 4));
        Assert.Equal(19, ListCursor.Target(7, ListMove.Bottom, 5, 20, 4));
        Assert.Equal(11, ListCursor.Target(7, ListMove.PageDown, 1, 20, 4));
        Assert.Equal(3, ListCursor.Target(7, ListMove.PageUp, 1, 20, 4));
        Assert.Equal(10, ListCursor.Target(7, ListMove.Down, 3, 20, 4));
    }

    [Theory]
    [InlineData(0, 5, 0)]
    [InlineData(2, 5, 0)]
    [InlineData(3, 5, 1)]
    [InlineData(9, 5, 5)]
    public void GivenAListOfTenInFiveRows_WhenFindingTheFirstRow_ThenTheSelectionKeepsTheScrollMargin(
        int selected,
        int viewport,
        int expected)
    {
        Assert.Equal(expected, ListCursor.FirstVisible(selected, 10, viewport));
    }

    [Fact]
    public void GivenTheStatelessAndTheStatefulForms_WhenScrollingDownFromTheTop_ThenTheyAgree()
    {
        var position = new ListPosition(0, 0);

        for (var step = 1; step < 30; step++)
        {
            position = ListCursor.Move(position, ListMove.Down, 1, 30, 8);

            Assert.Equal(position.Scroll, ListCursor.FirstVisible(position.Selected, 30, 8));
        }
    }

    [Fact]
    public void GivenAnEmptyListOrNoRows_WhenFindingTheFirstRow_ThenItIsZero()
    {
        Assert.Equal(0, ListCursor.FirstVisible(3, 0, 5));
        Assert.Equal(0, ListCursor.FirstVisible(3, 10, 0));
    }
}
