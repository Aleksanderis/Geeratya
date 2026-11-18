using Geeratya.Core.Editing;
using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

public sealed class CaretTextTests
{
    [Fact]
    public void GivenTheCaretInsideTheText_WhenBuildingSpans_ThenTheTextIsUnchangedAndTheCaretCoversACharacter()
    {
        var spans = CaretText.Spans(TextBuffer.FromBack("Fix the login redirect", 8), 40);

        Assert.Equal("Fix the login redirect", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal("r", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenTheCaretAtTheEnd_WhenBuildingSpans_ThenTheCaretIsASpaceCellAfterTheText()
    {
        var spans = CaretText.Spans(TextBuffer.FromBack("abc", 0), 40);

        Assert.Equal("abc ", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal(" ", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenAParagraphRowWithoutTheCaret_WhenBuildingSpans_ThenThereIsNoCaretCell()
    {
        var prompt = TextBuffer.FromBack("one\ntwo", 2);

        var other = CaretText.ParagraphSpans(prompt, "one", 0, 40);
        var caretRow = CaretText.ParagraphSpans(prompt, "two", 1, 40);

        Assert.DoesNotContain(other, static span => span.Role == TextRole.EditCaret);
        Assert.Equal("w", Assert.Single(caretRow, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenAWideCharacterUnderTheCaret_WhenBuildingSpans_ThenTheCaretCoversTheWholeCharacter()
    {
        var spans = CaretText.Spans(TextBuffer.FromBack("日本語", 2), 40);

        Assert.Equal("本", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenALongTextCutOnBothSides_WhenBuildingSpans_ThenTheCaretNeverSitsOnAnEllipsisAndTheRowFits()
    {
        const int width = 5;

        var text = new string('a', 10) + "X" + new string('b', 40);
        var spans = CaretText.Spans(TextBuffer.FromBack(text, 40), width);
        var caret = Assert.Single(spans, static span => span.Role == TextRole.EditCaret);

        Assert.NotEqual("\u2026", caret.Text);
        Assert.True(spans.Sum(static span => DisplayWidth.Of(span.Text)) <= width);
        Assert.EndsWith("\u2026", string.Concat(spans.Select(static span => span.Text)));
    }

    [Fact]
    public void GivenATextEndingInAnEllipsisCharacter_WhenTheCaretIsBeforeIt_ThenTheCaretCoversIt()
    {
        var spans = CaretText.Spans(TextBuffer.FromBack("a\u2026", 1), 40);

        Assert.Equal("\u2026", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenAPlainStringSurface_WhenMarkingTheCaret_ThenItIsAnUnderscoreAtTheEndAndABarInside()
    {
        Assert.Equal("abc_", CaretText.Marked(TextBuffer.Of("abc"), 20));
        Assert.Equal("ab|c", CaretText.Marked(TextBuffer.FromBack("abc", 1), 20));
    }

    [Fact]
    public void GivenTheCaretOnTheThirdLine_WhenBuildingParagraphRows_ThenOnlyThatRowHasTheCaretCell()
    {
        var text = TextBuffer.FromBack("one\ntwo\nthree", 3);

        var rows = text.Text.Split('\n')
            .Select((row, index) => CaretText.ParagraphSpans(text, row, index, 40))
            .ToList();

        Assert.Equal(2, text.Line);
        var flags = rows.Select(static row => row.Any(span => span.Role == TextRole.EditCaret)).ToList();

        Assert.True(flags is [false, false, true]);
        Assert.Equal("r", Assert.Single(rows[2], static span => span.Role == TextRole.EditCaret).Text);
    }
}
