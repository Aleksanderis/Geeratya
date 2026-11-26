using Geeratya.Core.Editing;

namespace Geeratya.Core.Tests.Editing;

[Trait("Os", "Independent")]
public class TextBufferSelectionTests
{
    [Fact]
    public void GivenTheCaretAtTheEnd_WhenSelectingLeftTwice_ThenTheLastTwoCharactersAreSelected()
    {
        var buffer = TextBuffer.Of("hello")
            .Apply(EditorOperation.SelectLeft)
            .Apply(EditorOperation.SelectLeft);

        Assert.True(buffer.HasSelection);
        Assert.Equal("lo", buffer.SelectedText);
        Assert.Equal(3, buffer.Cursor);
        Assert.Equal(5, buffer.Anchor);
    }

    [Fact]
    public void GivenASelection_WhenSelectingBackToTheAnchor_ThenNothingIsSelected()
    {
        var buffer = TextBuffer.Of("hello")
            .Apply(EditorOperation.SelectLeft)
            .Apply(EditorOperation.SelectRight);

        Assert.False(buffer.HasSelection);
        Assert.Equal(string.Empty, buffer.SelectedText);
        Assert.Null(buffer.Anchor);
    }

    [Fact]
    public void GivenTheCaretInTheMiddle_WhenSelectingRight_ThenTheSelectionGrowsAfterTheCaret()
    {
        var buffer = new TextBuffer("abcdef", 2).Apply(EditorOperation.SelectRight).Apply(EditorOperation.SelectRight);

        Assert.Equal("cd", buffer.SelectedText);
        Assert.Equal(2, buffer.SelectionStart);
        Assert.Equal(4, buffer.SelectionEnd);
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenSelecting_ThenTheSelectionHoldsWholeCharacters()
    {
        var buffer = TextBuffer.Of("a\U0001F600é").Apply(EditorOperation.SelectLeft).Apply(EditorOperation.SelectLeft);

        Assert.Equal("\U0001F600é", buffer.SelectedText);
    }

    [Fact]
    public void GivenSeveralWords_WhenSelectingByWord_ThenTheWordIsSelected()
    {
        var buffer = TextBuffer.Of("alpha beta").Apply(EditorOperation.SelectWordLeft);

        Assert.Equal("beta", buffer.SelectedText);
        Assert.Equal("alpha beta", buffer.Apply(EditorOperation.SelectWordLeft).Apply(EditorOperation.SelectWordLeft).SelectedText);
    }

    [Fact]
    public void GivenSeveralLines_WhenSelectingUp_ThenTheSelectionSpansTheLineBreak()
    {
        var buffer = new TextBuffer("one\ntwo\nthree", 9).Apply(EditorOperation.SelectUp);

        Assert.Equal("wo\nt", buffer.SelectedText);
    }

    [Fact]
    public void GivenSeveralLines_WhenSelectingToTheRowEnds_ThenOnlyThatRowIsCovered()
    {
        var middle = new TextBuffer("one\ntwo\nthree", 5);

        Assert.Equal("t", middle.Apply(EditorOperation.SelectRowStart).SelectedText);
        Assert.Equal("wo", middle.Apply(EditorOperation.SelectRowEnd).SelectedText);
    }

    [Fact]
    public void GivenAText_WhenSelectingAll_ThenEverythingIsSelectedAndAnEmptyTextStaysAsItIs()
    {
        var buffer = new TextBuffer("one\ntwo", 2).Apply(EditorOperation.SelectAll);

        Assert.Equal("one\ntwo", buffer.SelectedText);
        Assert.Equal(7, buffer.Cursor);
        Assert.Equal(TextBuffer.Empty, TextBuffer.Empty.Apply(EditorOperation.SelectAll));
    }

    [Fact]
    public void GivenASelection_WhenTyping_ThenTheTypedTextReplacesIt()
    {
        var buffer = TextBuffer.Of("hello world").Apply(EditorOperation.SelectWordLeft).Insert("there");

        Assert.Equal("hello there", buffer.Text);
        Assert.Equal(11, buffer.Cursor);
        Assert.False(buffer.HasSelection);
    }

    [Fact]
    public void GivenASelectionInTheMiddle_WhenTypingAPasteWithLineBreaks_ThenItReplacesTheSelection()
    {
        var buffer = TextBuffer.Of("abXYcd").Apply(EditorOperation.Left).Apply(EditorOperation.Left);
        var selected = buffer.Apply(EditorOperation.SelectLeft).Apply(EditorOperation.SelectLeft).Insert("1\n2");

        Assert.Equal("ab1\n2cd", selected.Text);
        Assert.Equal(5, selected.Cursor);
    }

    [Theory]
    [InlineData(EditorOperation.Backspace)]
    [InlineData(EditorOperation.Delete)]
    [InlineData(EditorOperation.DeleteWordBefore)]
    [InlineData(EditorOperation.DeleteWordAfter)]
    [InlineData(EditorOperation.DeleteToStart)]
    [InlineData(EditorOperation.DeleteToEnd)]
    public void GivenASelection_WhenDeleting_ThenOnlyTheSelectionGoes(EditorOperation operation)
    {
        var buffer = new TextBuffer("alpha beta gamma", 10).Apply(EditorOperation.SelectWordLeft);

        var deleted = buffer.Apply(operation);

        Assert.Equal("alpha  gamma", deleted.Text);
        Assert.Equal(6, deleted.Cursor);
        Assert.False(deleted.HasSelection);
    }

    [Fact]
    public void GivenASelection_WhenMovingLeftOrRight_ThenItCollapsesToItsStartOrEnd()
    {
        var selected = new TextBuffer("abcdef", 4).Apply(EditorOperation.SelectLeft).Apply(EditorOperation.SelectLeft);

        var left = selected.Apply(EditorOperation.Left);
        var right = selected.Apply(EditorOperation.Right);

        Assert.Equal((2, false), (left.Cursor, left.HasSelection));
        Assert.Equal((4, false), (right.Cursor, right.HasSelection));
    }

    [Fact]
    public void GivenASelection_WhenMovingByWordOrRow_ThenTheSelectionIsDropped()
    {
        var selected = TextBuffer.Of("one two").Apply(EditorOperation.SelectLeft);

        Assert.False(selected.Apply(EditorOperation.WordLeft).HasSelection);
        Assert.False(selected.Apply(EditorOperation.RowStart).HasSelection);
        Assert.False(selected.Apply(EditorOperation.Up).HasSelection);
    }

    [Fact]
    public void GivenASelectionAcrossAMention_WhenDeleting_ThenTheMentionGoesWholeAndSelectingStepsOverIt()
    {
        var text = "hi @[Olga](mention:abc) end";
        var rich = TextBuffer.Rich(text);
        var selected = rich
            .Apply(EditorOperation.SelectWordLeft)
            .Apply(EditorOperation.SelectLeft)
            .Apply(EditorOperation.SelectLeft);

        Assert.Equal(text[text.IndexOf('@')..], selected.SelectedText);
        Assert.Equal("hi ", selected.DeleteSelection().Text);
    }

    [Fact]
    public void GivenAFullBuffer_WhenTypingOverASelection_ThenTheRoomOfTheSelectionIsUsed()
    {
        var text = new string('a', TextBuffer.MaxLength);
        var selected = TextBuffer.Of(text).Apply(EditorOperation.SelectLeft).Apply(EditorOperation.SelectLeft);

        var typed = selected.Insert("xyz");

        Assert.Equal(TextBuffer.MaxLength, typed.Text.Length);
        Assert.EndsWith("xy", typed.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenNoSelection_WhenCopyingOrDeletingTheSelection_ThenNothingChanges()
    {
        var buffer = TextBuffer.Of("abc");

        Assert.Equal(string.Empty, buffer.SelectedText);
        Assert.Equal(buffer, buffer.DeleteSelection());
    }
}
