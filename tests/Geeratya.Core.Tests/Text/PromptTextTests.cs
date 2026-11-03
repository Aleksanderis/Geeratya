using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

[Trait("Os", "Independent")]
public class PromptTextTests
{
    [Fact]
    public void GivenTheCaretInTheMiddle_WhenInserting_ThenTheTextGoesInAtTheCaretAndTheCaretStaysAfterIt()
    {
        var prompt = PromptText.Of("abc").Apply(PromptOperation.Left).Insert("X");

        Assert.Equal("abXc", prompt.Text);
        Assert.Equal(3, prompt.Caret);
    }

    [Fact]
    public void GivenATextSetWithoutACaret_WhenInserting_ThenItAppendsAndTheCaretStaysAtTheEnd()
    {
        var prompt = PromptText.Of("ab").Insert("c");

        Assert.Equal("abc", prompt.Text);
        Assert.Equal(0, prompt.Back);
    }

    [Fact]
    public void GivenAStaleCaretBeyondTheText_WhenInserting_ThenTheCaretIsClampedToTheStart()
    {
        var prompt = new PromptText("ab", 9).Insert("X");

        Assert.Equal("Xab", prompt.Text);
    }

    [Fact]
    public void GivenTheCaretAtTheEnds_WhenMovingOrDeletingOutward_ThenNothingChanges()
    {
        var start = PromptText.Of("ab").Apply(PromptOperation.LineStart);
        var end = PromptText.Of("ab");

        Assert.Equal(start, start.Apply(PromptOperation.Left));
        Assert.Equal(start, start.Apply(PromptOperation.Backspace));
        Assert.Equal(end, end.Apply(PromptOperation.Right));
        Assert.Equal(end, end.Apply(PromptOperation.Delete));
        Assert.Equal(end, end.Apply(PromptOperation.DeleteToEnd));
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenMoving_ThenEachCharacterIsOneStep()
    {
        var text = "a\U0001F600é";

        var one = PromptText.Of(text).Apply(PromptOperation.Left);
        var two = one.Apply(PromptOperation.Left);

        Assert.Equal(3, one.Caret);
        Assert.Equal(1, two.Caret);
        Assert.Equal(3, two.Apply(PromptOperation.Right).Caret);
    }

    [Fact]
    public void GivenAnEmojiAndACombiningMark_WhenDeleting_ThenTheWholeCharacterGoes()
    {
        var text = "a\U0001F600é";

        Assert.Equal("a\U0001F600", PromptText.Of(text).Apply(PromptOperation.Backspace).Text);
        Assert.Equal("aé", PromptText.Of(text).Apply(PromptOperation.Left).Apply(PromptOperation.Left)
            .Apply(PromptOperation.Delete).Text);
    }

    [Fact]
    public void GivenAWideCharacter_WhenBackspacing_ThenTheWholeCharacterGoes()
    {
        var prompt = PromptText.Of("a日").Apply(PromptOperation.Backspace);

        Assert.Equal("a", prompt.Text);
    }

    [Fact]
    public void GivenAnInnerCaret_WhenBackspacingAndDeleting_ThenTheCharacterBeforeOrUnderTheCaretGoes()
    {
        var middle = PromptText.Of("abcd").Apply(PromptOperation.Left).Apply(PromptOperation.Left);

        Assert.Equal("acd", middle.Apply(PromptOperation.Backspace).Text);
        Assert.Equal(1, middle.Apply(PromptOperation.Backspace).Caret);
        Assert.Equal("abd", middle.Apply(PromptOperation.Delete).Text);
        Assert.Equal(2, middle.Apply(PromptOperation.Delete).Caret);
    }

    [Fact]
    public void GivenSeveralWords_WhenMovingByWord_ThenThePunctuationBetweenIsSkipped()
    {
        var prompt = PromptText.Of("alpha, beta  gamma");

        var first = prompt.Apply(PromptOperation.WordLeft);
        var second = first.Apply(PromptOperation.WordLeft);

        Assert.Equal(13, first.Caret);
        Assert.Equal(7, second.Caret);
        Assert.Equal(11, second.Apply(PromptOperation.WordRight).Caret);
        Assert.Equal(0, PromptText.Of("  ").Apply(PromptOperation.WordLeft).Caret);
    }

    [Fact]
    public void GivenTheCaretAfterAWord_WhenDeletingTheWordBefore_ThenOnlyThatWordGoes()
    {
        var prompt = PromptText.Of("open board now").Apply(PromptOperation.DeleteWordBefore);

        Assert.Equal("open board ", prompt.Text);
        Assert.Equal(11, prompt.Caret);
        Assert.Equal("open  now", new PromptText("open board now", 4).Apply(PromptOperation.DeleteWordBefore).Text);
    }

    [Fact]
    public void GivenTheCaretBeforeAWord_WhenDeletingTheWordAfter_ThenTheCaretStaysAndTheWordGoes()
    {
        var start = PromptText.Of("open board").Apply(PromptOperation.LineStart);

        var prompt = start.Apply(PromptOperation.DeleteWordAfter);

        Assert.Equal(" board", prompt.Text);
        Assert.Equal(0, prompt.Caret);
    }

    [Fact]
    public void GivenAnInnerCaret_WhenDeletingToTheStartOrEnd_ThenTheRestStays()
    {
        var inner = new PromptText("abcdef", 3);

        Assert.Equal("def", inner.Apply(PromptOperation.DeleteToStart).Text);
        Assert.Equal(0, inner.Apply(PromptOperation.DeleteToStart).Caret);
        Assert.Equal("abc", inner.Apply(PromptOperation.DeleteToEnd).Text);
    }

    [Fact]
    public void GivenALineBreak_WhenGoingToTheStartOrEnd_ThenTheCaretsLineIsMeant()
    {
        var onTwo = new PromptText("one\ntwo\nthree", 7);

        Assert.Equal(4, onTwo.Apply(PromptOperation.LineStart).Caret);
        Assert.Equal(7, onTwo.Apply(PromptOperation.LineEnd).Caret);
        Assert.Equal("one\nwo\nthree", onTwo.Apply(PromptOperation.LineStart).Apply(PromptOperation.Delete).Text);
        Assert.Equal("one\nt\nthree", new PromptText("one\ntwo\nthree", 8).Apply(PromptOperation.DeleteToEnd).Text);
    }
}
