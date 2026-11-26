using Geeratya.Core.Editing;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Editing;

public class TextPointingTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 3)]
    [InlineData(40, 11)]
    public void GivenAShortLine_WhenReadingACell_ThenItIsTheCharacterThere(int cell, int expected)
    {
        Assert.Equal(expected, TextPointing.IndexInLine(TextBuffer.Of("hello world"), 0, cell, 30));
    }

    [Fact]
    public void GivenAWideCharacter_WhenReadingEitherOfItsCells_ThenTheIndexIsItsStart()
    {
        var buffer = TextBuffer.Of("a日b");

        Assert.Equal(1, TextPointing.IndexInLine(buffer, 0, 1, 30));
        Assert.Equal(1, TextPointing.IndexInLine(buffer, 0, 2, 30));
        Assert.Equal(2, TextPointing.IndexInLine(buffer, 0, 3, 30));
    }

    [Fact]
    public void GivenALineCutAroundItsCaret_WhenReadingACell_ThenTheIndexMatchesTheDrawnText()
    {
        var text = new string('a', 30) + "XYZ" + new string('b', 30);
        var buffer = TextBuffer.Of(text) with { Cursor = 31 };
        var window = PromptWindow.Of(buffer, 20);
        var drawn = $"{window.Before}{window.After}";
        var cell = drawn.IndexOf('X', StringComparison.Ordinal);

        Assert.Equal(30, TextPointing.IndexInLine(buffer, 0, cell, 20));
    }

    [Fact]
    public void GivenSeveralLines_WhenReadingALine_ThenTheIndexIncludesTheLinesBefore()
    {
        var buffer = TextBuffer.Of("one\ntwo\nthree") with { Cursor = 0 };

        Assert.Equal(9, TextPointing.IndexInLine(buffer, 2, 1, 30));
    }

    [Fact]
    public void GivenAWrappedRow_WhenReadingACell_ThenTheIndexIsInThatRow()
    {
        var buffer = TextBuffer.Rich("aaaa bbbb cccc");

        Assert.Equal(5, TextPointing.IndexInRows(buffer, 1, 0, 6));
    }

    [Theory]
    [InlineData(1, "hello")]
    [InlineData(4, "hello")]
    [InlineData(6, "  ")]
    [InlineData(8, "world")]
    [InlineData(11, "world")]
    public void GivenAWordPointedAt_WhenDoubleClicking_ThenTheRunUnderItIsSelected(int index, string expected)
    {
        var selected = TextPointing.Word(TextBuffer.Of("hello  world"), index);

        Assert.Equal(expected, selected.SelectedText);
    }

    [Fact]
    public void GivenAMention_WhenDoubleClickingIt_ThenTheWholeMentionIsSelected()
    {
        var buffer = TextBuffer.Rich("hi @[Olga Other](mention:abc) there");

        Assert.Equal("@[Olga Other](mention:abc)", TextPointing.Word(buffer, 5).SelectedText);
    }

    [Fact]
    public void GivenAPointerMove_WhenItChangesTheCaret_ThenTheTypedUndoStepEnds()
    {
        var typed = TextBuffer.Empty.Insert("a").Insert("b");
        var moved = TextPointing.Placed(typed, 0).Insert("c");

        Assert.Equal("cab", moved.Text);
        Assert.Equal("ab", moved.Apply(EditorOperation.Undo).Text);
    }

    [Fact]
    public void GivenADragOfNoLength_WhenDragging_ThenNothingIsSelected()
    {
        var dragged = TextPointing.Dragged(TextBuffer.Of("abc"), 1, 1);

        Assert.False(dragged.HasSelection);
        Assert.Equal(1, dragged.Cursor);
    }
}
