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
}
