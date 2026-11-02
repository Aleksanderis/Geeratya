using Geeratya.Core.Editing;

namespace Geeratya.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class TextBufferTests
{
    private const int Wide = 40;

    [Fact]
    public void GivenAnEmptyBuffer_WhenInsertingAndDeleting_ThenItNeverGoesOutOfRange()
    {
        var empty = TextBuffer.Empty;

        Assert.Equal(empty, empty.Backspace());
        Assert.Equal(empty, empty.Delete());
        Assert.Equal(empty, empty.Left());
        Assert.Equal(empty, empty.Right());
        Assert.Equal(empty, empty.Vertical(1, Wide));
        Assert.Equal(empty, empty.RowStart(Wide));
        Assert.Equal(empty, empty.RowEnd(Wide));
        Assert.Equal(empty, empty.Insert(string.Empty));
    }

    [Fact]
    public void GivenTextInTheMiddle_WhenInsertingBackspacingAndDeleting_ThenTheCursorFollows()
    {
        var buffer = new TextBuffer("abcd", 2);

        var inserted = buffer.Insert("XY");
        var backspaced = inserted.Backspace();
        var deleted = backspaced.Delete();

        Assert.Equal(new TextBuffer("abXYcd", 4), inserted);
        Assert.Equal(new TextBuffer("abXcd", 3), backspaced);
        Assert.Equal(new TextBuffer("abXd", 3), deleted);
    }

    [Theory]
    [InlineData("a😀", 3)]
    [InlineData("ae\u0301", 3)]
    [InlineData("a👨‍👩‍👧", 9)]
    public void GivenAnEmojiOrCombiningSequenceAtTheEnd_WhenBackspacing_ThenItGoesAsOneCharacter(string text, int length)
    {
        Assert.Equal(length, text.Length);

        var buffer = TextBuffer.Of(text).Backspace();

        Assert.Equal("a", buffer.Text);
        Assert.Equal(1, buffer.Cursor);
    }

    [Fact]
    public void GivenAnEmojiUnderTheCursor_WhenMovingAndDeleting_ThenItIsOneStep()
    {
        var buffer = new TextBuffer("a😀b", 1);

        Assert.Equal(3, buffer.Right().Cursor);
        Assert.Equal(1, buffer.Right().Left().Cursor);
        Assert.Equal("ab", buffer.Delete().Text);
    }

    [Fact]
    public void GivenAFullBuffer_WhenInserting_ThenTheTextIsCutWithoutHalfASurrogatePair()
    {
        var full = TextBuffer.Of(new string('a', TextBuffer.MaxLength - 1));

        var inserted = full.Insert("😀x");

        Assert.Equal(TextBuffer.MaxLength - 1, inserted.Text.Length);
        Assert.False(char.IsHighSurrogate(inserted.Text[^1]));

        var filled = full.Insert("x");

        Assert.Equal(TextBuffer.MaxLength, filled.Text.Length);
        Assert.Same(filled, filled.Insert("y"));
    }

    [Fact]
    public void GivenTwoLines_WhenMovingUpAndDown_ThenTheDisplayColumnIsKept()
    {
        var buffer = new TextBuffer("abcdef\nxy\nabcdef", 5);

        var down = buffer.Vertical(1, Wide);
        var downAgain = down.Vertical(1, Wide);
        var up = downAgain.Vertical(-1, Wide);

        Assert.Equal(9, down.Cursor);
        Assert.Equal(12, downAgain.Cursor);
        Assert.Equal(9, up.Cursor);
        Assert.Equal(buffer, buffer.Vertical(-1, Wide));
        Assert.Equal(downAgain, downAgain.Vertical(1, Wide));
    }

    [Fact]
    public void GivenWideCharacters_WhenMovingUp_ThenTheColumnIsMeasuredInDisplayCells()
    {
        var buffer = new TextBuffer("abcdef\n日本語", 3);

        var down = buffer.Vertical(1, Wide);

        // Three cells of "abc" hold one wide character (two cells) and not the second one (cells 2 to 4).
        Assert.Equal(8, down.Cursor);
    }

    [Fact]
    public void GivenALongLineThatWraps_WhenMovingDownAndUp_ThenTheCursorMovesByVisualRow()
    {
        var buffer = new TextBuffer("abcdefghijkl", 2);

        var down = buffer.Vertical(1, 5);
        var up = down.Vertical(-1, 5);

        Assert.Equal(7, down.Cursor);
        Assert.Equal(2, up.Cursor);
        Assert.Equal(4, buffer.RowEnd(5).Cursor);
        Assert.Equal(0, buffer.RowStart(5).Cursor);
        Assert.Equal(5, down.RowStart(5).Cursor);
    }

    [Fact]
    public void GivenAWrappedRow_WhenPressingEnd_ThenTheCursorStaysOnThatRowAndRightEntersTheNextOne()
    {
        var buffer = new TextBuffer("abcdefghijkl", 1);

        var end = buffer.RowEnd(5);
        var next = end.Right();

        Assert.Equal(4, end.Cursor);
        Assert.Equal(5, next.Cursor);
        Assert.Equal(10, next.Vertical(1, 5).Cursor);
    }
}
