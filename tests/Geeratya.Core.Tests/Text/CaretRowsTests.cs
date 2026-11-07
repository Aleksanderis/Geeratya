using Geeratya.Core.Text;

namespace Geeratya.Core.Tests.Text;

public sealed class CaretRowsTests
{
    [Fact]
    public void GivenTheCaretInsideTheText_WhenBuildingSpans_ThenTheTextIsUnchangedAndTheCaretCoversACharacter()
    {
        var spans = CaretRows.Spans(new PromptText("Fix the login redirect", 8), 40, TextRole.Editable);

        Assert.Equal("Fix the login redirect", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal("r", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenTheCaretAtTheEnd_WhenBuildingSpans_ThenTheCaretIsASpaceCellAfterTheText()
    {
        var spans = CaretRows.Spans(new PromptText("abc", 0), 40, TextRole.Editable);

        Assert.Equal("abc ", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal(" ", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenAParagraphRowWithoutTheCaret_WhenBuildingSpans_ThenThereIsNoCaretCell()
    {
        var prompt = new PromptText("one\ntwo", 2);

        var other = CaretRows.ParagraphSpans(prompt, "one", 0, 1, 40, TextRole.Editable);
        var caretRow = CaretRows.ParagraphSpans(prompt, "two", 1, 1, 40, TextRole.Editable);

        Assert.DoesNotContain(other, static span => span.Role == TextRole.EditCaret);
        Assert.Equal("w", Assert.Single(caretRow, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenAWideCharacterUnderTheCaret_WhenBuildingSpans_ThenTheCaretCoversTheWholeCharacter()
    {
        var spans = CaretRows.Spans(new PromptText("日本語", 2), 40, TextRole.Editable);

        Assert.Equal("本", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Fact]
    public void GivenALongTextCutOnBothSides_WhenBuildingSpans_ThenTheCaretNeverSitsOnAnEllipsisAndTheRowFits()
    {
        const int width = 5;

        var text = new string('a', 10) + "X" + new string('b', 40);
        var spans = CaretRows.Spans(new PromptText(text, 40), width, TextRole.Editable);
        var caret = Assert.Single(spans, static span => span.Role == TextRole.EditCaret);

        Assert.NotEqual("\u2026", caret.Text);
        Assert.True(spans.Sum(static span => DisplayWidth.Of(span.Text)) <= width);
        Assert.EndsWith("\u2026", string.Concat(spans.Select(static span => span.Text)));
    }

    [Fact]
    public void GivenATextEndingInAnEllipsisCharacter_WhenTheCaretIsBeforeIt_ThenTheCaretCoversIt()
    {
        var spans = CaretRows.Spans(new PromptText("a\u2026", 1), 40, TextRole.Editable);

        Assert.Equal("\u2026", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }
}
