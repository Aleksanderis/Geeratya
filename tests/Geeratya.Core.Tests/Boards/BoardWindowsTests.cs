using Geeratya.Core.Boards;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardWindowsTests
{
    [Theory]
    [InlineData(4, 40, 1)]
    [InlineData(4, 80, 4)]
    [InlineData(4, 120, 4)]
    [InlineData(4, 200, 4)]
    [InlineData(12, 40, 1)]
    [InlineData(4, 59, 1)]
    [InlineData(4, 60, 3)]
    [InlineData(12, 80, 4)]
    [InlineData(12, 120, 6)]
    [InlineData(12, 200, 10)]
    public void GivenWidthAndColumns_WhenChoosingTheWindow_ThenAsManyColumnsAsFitAtTheMinimumWidth(
        int total,
        int width,
        int expected)
    {
        var window = BoardWindows.For(total, 0, width);

        Assert.Equal(expected, window.Count);
        Assert.Equal(Enumerable.Range(0, window.Count).Sum(window.ColumnWidth), width);
        Assert.All(Enumerable.Range(0, window.Count), position => Assert.True(window.ColumnWidth(position) >= 20));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(5, 4)]
    [InlineData(11, 9)]
    public void GivenTwelveColumnsInThree_WhenSelecting_ThenTheWindowCentresAndStaysInsideTheBoard(
        int selected,
        int expectedFirst)
    {
        var window = BoardWindows.For(12, selected, 60);

        Assert.Equal(3, window.Count);
        Assert.Equal(expectedFirst, window.First);
        Assert.InRange(selected, window.First, window.First + window.Count - 1);
        Assert.Equal(12 - window.First - window.Count, window.HiddenRight);
        Assert.Equal(window.First, window.HiddenLeft);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(19)]
    [InlineData(20)]
    [InlineData(39)]
    public void GivenTinyWidths_WhenChoosing_ThenOneColumnAtMostTheWholeWidthAndNoCrash(int width)
    {
        var window = BoardWindows.For(4, 3, width);

        Assert.Equal(1, window.Count);
        Assert.Equal(3, window.First);
        Assert.Equal(width, window.ColumnWidth(0));
    }

    [Fact]
    public void GivenNoColumns_WhenChoosing_ThenTheWindowIsEmpty()
    {
        var window = BoardWindows.For(0, 0, 80);

        Assert.Equal(0, window.Count);
        Assert.Equal(0, window.ColumnWidth(0));
    }
}
