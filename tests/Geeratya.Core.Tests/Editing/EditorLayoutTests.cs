using Geeratya.Jira.Adf;
using Geeratya.Core.Editing;
using Geeratya.Core.Text;
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
    public void GivenAWordThatDoesNotFit_WhenLayingOut_ThenItWrapsWholeAfterTheLastSpace()
    {
        var rows = EditorLayout.Rows("aa bbb cc", 5);

        Assert.Equal([new EditorRow(0, 3), new EditorRow(3, 7), new EditorRow(7, 9)], rows);
    }

    [Fact]
    public void GivenAWordLongerThanTheRow_WhenLayingOut_ThenItWrapsByCharacterAfterTheSpaceWrap()
    {
        var rows = EditorLayout.Rows("a bcdefghij", 4);

        Assert.Equal([new EditorRow(0, 2), new EditorRow(2, 6), new EditorRow(6, 10), new EditorRow(10, 11)], rows);
    }

    [Fact]
    public void GivenASpaceThatDoesNotFit_WhenLayingOut_ThenItHangsAtTheEndOfItsRowAndNoRowIsBlank()
    {
        Assert.Equal([new EditorRow(0, 5), new EditorRow(5, 7)], EditorLayout.Rows("aaaa bb", 4));
        Assert.Equal([new EditorRow(0, 5), new EditorRow(5, 9)], EditorLayout.Rows("aaaa bbbb", 4));
    }

    [Theory]
    [InlineData("a bbb\u4e16\u4e16 c")]
    [InlineData(" bbb\u4e16")]
    [InlineData("aa  bbb\u4e16 x\u4e16")]
    public void GivenWideCharactersAfterWordWraps_WhenLayingOut_ThenNoRowIsWiderThanTheCapacity(string text)
    {
        var rows = EditorLayout.Rows(text, 4);

        Assert.All(rows, row => Assert.True(DisplayWidth.Of(text[row.Start..row.End].TrimEnd(' ')) <= 4));
    }

    [Fact]
    public void GivenAWordWrappedRow_WhenLocatingAtItsBoundary_ThenTheIndexBelongsToTheNextRow()
    {
        const string text = "aa bbb cc";
        var rows = EditorLayout.Rows(text, 5);

        Assert.Equal(new EditorPosition(1, 0), EditorLayout.Locate(text, rows, 3));
        Assert.Equal(new EditorPosition(0, 2), EditorLayout.Locate(text, rows, 2));
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
    public void GivenAMention_WhenLayingOut_ThenItTakesTheWidthOfItsShortFormAndStaysWhole()
    {
        var text = $"ab {AdfMarkdownTokens.Mention("acc-1", "Olga")} cd";

        var rows = EditorLayout.Rows(text, 20);
        var cursorAfter = EditorLayout.Locate(text, rows, text.Length - 3);

        Assert.Single(rows);
        Assert.Equal(new EditorPosition(0, 3 + "@Olga".Length), cursorAfter);
    }

    [Fact]
    public void GivenAMentionWithWideName_WhenItDoesNotFit_ThenItWrapsWholeToTheNextRow()
    {
        var token = AdfMarkdownTokens.Mention("acc-1", "日本語");
        var text = $"abcd {token}";

        var rows = EditorLayout.Rows(text, 8);

        Assert.Equal([new EditorRow(0, 5), new EditorRow(5, text.Length)], rows);
        Assert.Equal(new EditorPosition(1, 7), EditorLayout.Locate(text, rows, text.Length));
    }

    [Fact]
    public void GivenALinkInTheRowAbove_WhenMovingDown_ThenTheColumnIsMeasuredInShortCells()
    {
        var link = AdfMarkdownTokens.IssueLink("https://x.atlassian.net", "GCM-12");
        var text = $"{link} x\nabcdefgh";
        var buffer = new TextBuffer(text, text.IndexOf(" x", StringComparison.Ordinal) + 2);

        var down = buffer.Vertical(1, 30);

        Assert.Equal(text.IndexOf("abcdefgh", StringComparison.Ordinal) + "GCM-12 x".Length, down.Cursor);
    }

    [Fact]
    public void GivenAMention_WhenListingCells_ThenTheSavedTextIsUnchangedAndTheCellShowsTheShortForm()
    {
        var token = AdfMarkdownTokens.Mention("acc-1", "Olga");
        var cells = EditorLayout.Cells($"a{token}");

        Assert.Equal(["a", "@Olga"], cells.Select(cell => cell.Display));
        Assert.Equal(new EditorCell(1, 1 + token.Length, "@Olga"), cells[1]);
    }

    [Fact]
    public void GivenPastedWindowsText_WhenCleaning_ThenLinesAreKeptTabsBecomeSpacesAndControlsGo()
    {
        var pasted = string.Concat("one", (char)13, (char)10, "two", (char)13, "three", (char)9, "x", (char)27, "[31m", (char)7, "end", (char)10);

        var cleaned = PasteText.Lines(pasted);

        Assert.Equal("one\ntwo\nthree    x[31mend\n", cleaned);
    }
}
