using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>The one look of typed text, placeholders, key labels, errors and the caret across surfaces (#996).</summary>
public class SharedLookTests
{
    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

    [Fact]
    public void GivenAHelpPage_WhenBuildingTheStyledFrame_ThenKeyLabelsUseTheKeyRole()
    {
        var spans = Spans(HelpPageSnapshotTests.StateFor("list"), 120, 36);

        Assert.Contains(spans, span => span.Role == TextRole.Key);
    }

    [Fact]
    public void GivenThePalette_WhenBuildingTheStyledFrame_ThenShortcutsUseTheKeyRole()
    {
        var spans = Spans(PaletteOverlayTests.Sample(string.Empty), 120, 36);

        Assert.Contains(spans, span => span.Role == TextRole.Key && span.Text.Trim().Length > 0);
    }

    [Fact]
    public void GivenEmptyUnfocusedFields_WhenBuildingTheStyledFrame_ThenPlaceholdersAreMutedWords()
    {
        var labels = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, []);
        var assignee = FormField.Create("assignee", "Assignee", FormFieldKind.User, false, []);
        var summary = FormField.Create("summary", "Summary", FormFieldKind.Text, false, []);
        var form = FormOverlaySnapshotTests.SampleForm() with { Fields = [summary, labels, assignee], Focus = 0 };

        var spans = Spans(FormOverlaySnapshotTests.WithForm(form), 120, 36);

        Assert.Contains(spans, span => span.Role == TextRole.Muted && span.Text == EmptyValues.None);
        Assert.Contains(spans, span => span.Role == TextRole.Muted && span.Text == EmptyValues.Unassigned);
        Assert.DoesNotContain(spans, span => span.Role == TextRole.Editable && span.Text is "-" or "None");
    }

    [Fact]
    public void GivenALongFieldError_WhenBuildingTheFrame_ThenItWrapsAndTheSentenceIsNeverCut()
    {
        var error = "Estimate must be a number such as 0.5 or 3 and cannot be negative or contain any letters at all";
        var field = FormField.Create("estimate", "Estimate", FormFieldKind.Number, false, []) with { Error = error };
        var form = FormOverlaySnapshotTests.SampleForm() with { Fields = [field], Focus = 0 };

        var lines = FrameBuilder.Build(FormOverlaySnapshotTests.WithForm(form), 60, 24, _keymap);

        Assert.Contains(lines, line => line.Contains("! Estimate must", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("cannot be negative", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Contains("  all  ", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.Contains('…') && line.Contains('!'));
    }

    [Fact]
    public void GivenTheFocusedBoxedField_WhenBuildingTheFrame_ThenItsLabelCarriesTheFocusMark()
    {
        var lines = FrameBuilder.Build(FormOverlaySnapshotTests.Sample(), 120, 36, _keymap);

        Assert.Single(lines, line => line.Contains("┌─ > ", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("nord", Ansi16Color.White)]
    [InlineData("catppuccin-latte", Ansi16Color.Black)]
    public void GivenSixteenColours_WhenResolvingTheCaret_ThenItIsABlockInTheTextGround(string name, Ansi16Color fill)
    {
        var theme = BuiltInThemes.Find(name)!.Theme!;
        var caret = Palette.Resolve(theme, ColorSupport.Basic16, PaletteRole.EditCaret);

        Assert.Equal(fill, caret.Background16);
        Assert.NotEqual(caret.Foreground16, caret.Background16);
    }

    private static List<StyledSpan> Spans(AppState state, int width, int height)
    {
        return [.. FrameBuilder.BuildStyled(state, width, height, _keymap)!.Body.SelectMany(line => line.Spans)];
    }
}
