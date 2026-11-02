using Geeratya.Core.Editing;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class EditorLayoutTests
{
    [Fact]
    public void GivenEmptyText_WhenLayingOut_ThenThereIsOneEmptyRow()
    {
        var rows = EditorLayout.Rows(string.Empty, 10);

        Assert.Equal(new EditorRow(0, 0), Assert.Single(rows));
    }

    [Fact]
    public void GivenNewlines_WhenLayingOut_ThenEachLineIsARowAndBlankLinesKeepTheirRow()
    {
        var rows = EditorLayout.Rows("a\n\nb\n", 10);

        Assert.Equal([new EditorRow(0, 1), new EditorRow(2, 2), new EditorRow(3, 4), new EditorRow(5, 5)], rows);
    }

    [Fact]
    public void GivenWideCharacters_WhenAWideOneDoesNotFit_ThenItMovesToTheNextRowWhole()
    {
        var rows = EditorLayout.Rows("abc日本", 4);

        Assert.Equal([new EditorRow(0, 3), new EditorRow(3, 5)], rows);
    }

    [Fact]
    public void GivenACellWiderThanTheCapacity_WhenLayingOut_ThenItGetsARowOfItsOwnAndLayoutTerminates()
    {
        var rows = EditorLayout.Rows("日日", 1);

        Assert.Equal([new EditorRow(0, 1), new EditorRow(1, 2)], rows);
    }

    [Fact]
    public void GivenAWrappedRowBoundary_WhenLocating_ThenTheIndexBelongsToTheNextRow()
    {
        const string text = "abcdefgh";
        var rows = EditorLayout.Rows(text, 4);

        Assert.Equal(new EditorPosition(0, 3), EditorLayout.Locate(text, rows, 3));
        Assert.Equal(new EditorPosition(1, 0), EditorLayout.Locate(text, rows, 4));
        Assert.Equal(new EditorPosition(1, 4), EditorLayout.Locate(text, rows, 8));
    }

    [Fact]
    public void GivenAnIndexBeforeANewline_WhenLocating_ThenItStaysOnItsRow()
    {
        const string text = "ab\ncd";
        var rows = EditorLayout.Rows(text, 10);

        Assert.Equal(new EditorPosition(0, 2), EditorLayout.Locate(text, rows, 2));
        Assert.Equal(new EditorPosition(1, 0), EditorLayout.Locate(text, rows, 3));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(24, 23)]
    public void GivenABoxWidth_WhenAskingForTheCapacity_ThenOneCellIsKeptForTheCursor(int inner, int capacity)
    {
        Assert.Equal(capacity, EditorLayout.Capacity(inner));
    }

    [Fact]
    public void GivenPastedWindowsText_WhenCleaning_ThenLinesAreKeptTabsBecomeSpacesAndControlsGo()
    {
        var cleaned = PasteText.Lines("one\r\ntwo\rthree\tx\u001b[31m\u0007end\n");

        Assert.Equal("one\ntwo\nthree    x[31mend\n", cleaned);
    }
}
