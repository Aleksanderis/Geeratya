using Geeratya.Core.Editing;

namespace Geeratya.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class TextBufferOperationTests
{
    [Fact]
    public void GivenTheCaretInTheMiddle_WhenInserting_ThenTheTextGoesInAtTheCaretAndTheCaretStaysAfterIt()
    {
        var prompt = TextBuffer.Of("abc").Apply(EditorOperation.Left).Insert("X");

        Assert.Equal("abXc", prompt.Text);
        Assert.Equal(3, prompt.Cursor);
    }

    [Fact]
    public void GivenATextSetWithoutACaret_WhenInserting_ThenItAppendsAndTheCaretStaysAtTheEnd()
    {
        var prompt = TextBuffer.Of("ab").Insert("c");

        Assert.Equal("abc", prompt.Text);
        Assert.Equal(0, prompt.Back);
    }

    [Fact]
    public void GivenAStaleCaretBeyondTheText_WhenInserting_ThenTheCaretIsClampedToTheStart()
    {
        var prompt = TextBuffer.FromBack("ab", 9).Insert("X");

        Assert.Equal("Xab", prompt.Text);
    }

    [Fact]
    public void GivenTheCaretAtTheEnds_WhenMovingOrDeletingOutward_ThenNothingChanges()
    {
        var start = TextBuffer.Of("ab").Apply(EditorOperation.RowStart);
        var end = TextBuffer.Of("ab");

        Assert.Equal(start, start.Apply(EditorOperation.Left));
        Assert.Equal(start, start.Apply(EditorOperation.Backspace));
        Assert.Equal(end, end.Apply(EditorOperation.Right));
        Assert.Equal(end, end.Apply(EditorOperation.Delete));
        Assert.Equal(end, end.Apply(EditorOperation.DeleteToEnd));
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenMoving_ThenEachCharacterIsOneStep()
    {
        var text = "a\U0001F600é";

        var one = TextBuffer.Of(text).Apply(EditorOperation.Left);
        var two = one.Apply(EditorOperation.Left);

        Assert.Equal(3, one.Cursor);
        Assert.Equal(1, two.Cursor);
        Assert.Equal(3, two.Apply(EditorOperation.Right).Cursor);
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenDeleting_ThenTheWholeCharacterGoes()
    {
        var text = "a\U0001F600é";

        Assert.Equal("a\U0001F600", TextBuffer.Of(text).Apply(EditorOperation.Backspace).Text);
        Assert.Equal("aé", TextBuffer.Of(text).Apply(EditorOperation.Left).Apply(EditorOperation.Left)
            .Apply(EditorOperation.Delete).Text);
    }

    [Fact]
    public void GivenAWideCharacter_WhenBackspacing_ThenTheWholeCharacterGoes()
    {
        var prompt = TextBuffer.Of("a日").Apply(EditorOperation.Backspace);

        Assert.Equal("a", prompt.Text);
    }

    [Fact]
    public void GivenAnInnerCaret_WhenBackspacingAndDeleting_ThenTheCharacterBeforeOrUnderTheCaretGoes()
    {
        var middle = TextBuffer.Of("abcd").Apply(EditorOperation.Left).Apply(EditorOperation.Left);

        Assert.Equal("acd", middle.Apply(EditorOperation.Backspace).Text);
        Assert.Equal(1, middle.Apply(EditorOperation.Backspace).Cursor);
        Assert.Equal("abd", middle.Apply(EditorOperation.Delete).Text);
        Assert.Equal(2, middle.Apply(EditorOperation.Delete).Cursor);
    }

    [Fact]
    public void GivenSeveralWords_WhenMovingByWord_ThenThePunctuationBetweenIsSkipped()
    {
        var prompt = TextBuffer.Of("alpha, beta  gamma");

        var first = prompt.Apply(EditorOperation.WordLeft);
        var second = first.Apply(EditorOperation.WordLeft);

        Assert.Equal(13, first.Cursor);
        Assert.Equal(7, second.Cursor);
        Assert.Equal(11, second.Apply(EditorOperation.WordRight).Cursor);
        Assert.Equal(0, TextBuffer.Of("  ").Apply(EditorOperation.WordLeft).Cursor);
    }

    [Fact]
    public void GivenTheCaretAfterAWord_WhenDeletingTheWordBefore_ThenOnlyThatWordGoes()
    {
        var prompt = TextBuffer.Of("open board now").Apply(EditorOperation.DeleteWordBefore);

        Assert.Equal("open board ", prompt.Text);
        Assert.Equal(11, prompt.Cursor);
        var inner = TextBuffer.FromBack("open board now", 4);

        Assert.Equal("open  now", inner.Apply(EditorOperation.DeleteWordBefore).Text);
    }

    [Fact]
    public void GivenTheCaretBeforeAWord_WhenDeletingTheWordAfter_ThenTheCaretStaysAndTheWordGoes()
    {
        var start = TextBuffer.Of("open board").Apply(EditorOperation.RowStart);

        var prompt = start.Apply(EditorOperation.DeleteWordAfter);

        Assert.Equal(" board", prompt.Text);
        Assert.Equal(0, prompt.Cursor);
    }

    [Fact]
    public void GivenAnInnerCaret_WhenDeletingToTheStartOrEnd_ThenTheRestStays()
    {
        var inner = TextBuffer.FromBack("abcdef", 3);

        Assert.Equal("def", inner.Apply(EditorOperation.DeleteToStart).Text);
        Assert.Equal(0, inner.Apply(EditorOperation.DeleteToStart).Cursor);
        Assert.Equal("abc", inner.Apply(EditorOperation.DeleteToEnd).Text);
    }

    [Fact]
    public void GivenALineBreak_WhenGoingToTheStartOrEnd_ThenTheCaretsLineIsMeant()
    {
        var onTwo = TextBuffer.FromBack("one\ntwo\nthree", 7);

        Assert.Equal(4, onTwo.Apply(EditorOperation.RowStart).Cursor);
        Assert.Equal(7, onTwo.Apply(EditorOperation.RowEnd).Cursor);
        Assert.Equal("one\nwo\nthree", onTwo.Apply(EditorOperation.RowStart).Apply(EditorOperation.Delete).Text);
        var afterT = TextBuffer.FromBack("one\ntwo\nthree", 8);

        Assert.Equal("one\nt\nthree", afterT.Apply(EditorOperation.DeleteToEnd).Text);
    }

    [Theory]
    [InlineData("abc\nde\nfghij", 12, -1, 6)] // end of "fghij" -> column 5 clamps to the end of "de"
    [InlineData("abc\nde\nfghij", 6, -1, 2)] // end of "de" (column 2) -> column 2 of "abc"
    [InlineData("abc\nde\nfghij", 1, 1, 5)] // column 1 of "abc" -> column 1 of "de"
    [InlineData("abc\nde\nfghij", 5, 1, 8)] // column 1 of "de" -> column 1 of "fghij"
    [InlineData("abc\nde", 5, 1, 6)] // last line: down goes to the end of the text
    [InlineData("abc\nde", 1, -1, 0)] // first line: up goes to the start
    [InlineData("abc\nde\nfghij", 1, 2, 8)] // two lines down
    public void GivenTextWithLines_WhenMovingLines_ThenTheCaretKeepsItsColumnOrClampsToTheLine(
        string text,
        int caret,
        int lines,
        int expected)
    {
        var prompt = TextBuffer.FromBack(text, text.Length - caret).Vertical(lines, TextBuffer.NoWrap);

        Assert.Equal(expected, prompt.Cursor);
        Assert.Equal(text, prompt.Text);
    }

    [Fact]
    public void GivenAnEmojiAtTheTargetColumn_WhenMovingLines_ThenTheCaretDoesNotLandInsideIt()
    {
        var text = "abc\n\U0001F600\U0001F600";
        var prompt = TextBuffer.FromBack(text, text.Length - 1).Vertical(1, TextBuffer.NoWrap);

        Assert.Equal(4, prompt.Cursor);
    }
}
