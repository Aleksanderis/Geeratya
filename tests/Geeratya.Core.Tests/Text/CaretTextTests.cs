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

    [Fact]
    public void GivenASelection_WhenBuildingSpans_ThenTheSelectedTextIsInTheSelectionRoleAndTheCaretCellKeepsItsOwn()
    {
        var buffer = TextBuffer.Of("alpha beta").Apply(EditorOperation.SelectWordLeft);

        var spans = CaretText.Spans(buffer, 40);

        Assert.Equal("alpha beta", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal("b", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
        Assert.Equal("eta", string.Concat(spans.Where(static span => span.Role == CaretText.SelectedRole).Select(static span => span.Text)));
    }

    [Fact]
    public void GivenASelectionAcrossRows_WhenBuildingParagraphSpans_ThenEveryRowDrawsItsPartOfTheSelection()
    {
        var buffer = new TextBuffer("one\ntwo\nthree", 9, false, 2);

        var first = CaretText.ParagraphSpans(buffer, "one", 0, 40);
        var second = CaretText.ParagraphSpans(buffer, "two", 1, 40);
        var third = CaretText.ParagraphSpans(buffer, "three", 2, 40);

        Assert.Equal("e", SelectedOf(first));
        Assert.Equal("two", SelectedOf(second));
        Assert.Equal("t", SelectedOf(third));
        Assert.DoesNotContain(second, static span => span.Role == TextRole.EditCaret);
    }

    [Fact]
    public void GivenALongSelectedRowCutOnBothSides_WhenBuildingSpans_ThenOnlyVisibleSelectedTextIsMarkedAndTheRowFits()
    {
        var text = "0123456789abcdefghijklmnopqrstuvwxyz";
        var buffer = new TextBuffer(text, 20, false, 14);

        var spans = CaretText.Spans(buffer, 12);

        var shown = string.Concat(spans.Select(static span => span.Text));
        var selected = SelectedOf(spans);

        Assert.True(shown.Length <= 12);
        Assert.NotEmpty(selected);
        Assert.Contains(selected, text, StringComparison.Ordinal);
        Assert.DoesNotContain("…", selected, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenCellsAndASelection_WhenOverCells_ThenSelectedCellsAreMarkedAndTheCaretCellIsNot()
    {
        var cells = EditorLayout.RowCells("abcd", new EditorRow(0, 4)).ToList();

        var spans = CaretText.OverCells(cells, 1, 1, 3);

        Assert.Equal("abcd", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal("b", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
        Assert.Equal("c", SelectedOf(spans));
    }

    [Fact]
    public void GivenTheCaretPastTheLastCell_WhenOverCells_ThenATrailingSpaceCarriesTheCaret()
    {
        var cells = EditorLayout.RowCells("ab", new EditorRow(0, 2)).ToList();

        var spans = CaretText.OverCells(cells, 2, 0, 0);

        Assert.Equal("ab ", string.Concat(spans.Select(static span => span.Text)));
        Assert.Equal(" ", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
        Assert.DoesNotContain(CaretText.OverCells(cells, -1, 0, 0), static span => span.Role == TextRole.EditCaret);
    }

    private static string SelectedOf(IReadOnlyList<StyledSpan> spans)
    {
        return string.Concat(spans.Where(static span => span.Role == CaretText.SelectedRole).Select(static span => span.Text));
    }
}
