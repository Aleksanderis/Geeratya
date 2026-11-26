using Geeratya.Core.Clipboard;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>
/// One scenario set over every text surface that selects (the Summary text field, the Description paragraph field and
/// the comment editor): the same keys select, replace, delete, copy and cut the same text, and the selected text is
/// drawn in <see cref="CaretText.SelectedRole"/> with the caret cell over it. A new selecting surface is added to
/// <see cref="Surfaces"/>.
/// </summary>
[Trait("Os", "Independent")]
public class TextSelectionContractTests
{
    private const string Words = "alpha beta gamma";

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _now = new(2026, 11, 26, 12, 0, 0, TimeSpan.Zero);

    private readonly AppReducer _reducer = new(_keymap);

    public static TheoryData<string> Surfaces()
    {
        return ["form-text", "form-paragraph", "comment"];
    }

    public static TheoryData<string> LineSurfaces()
    {
        return ["form-paragraph", "comment"];
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenShiftLeft_WhenPressedThreeTimes_ThenTheLastThreeCharactersAreSelected(string surface)
    {
        var state = Keys(Open(surface), [.. Type("abcdef"), "Shift-Left", "Shift-Left", "Shift-Left"]);

        Assert.Equal("def", Selected(surface, state));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenShiftRightAndHomeEnd_WhenSelecting_ThenTheSelectionFollowsTheCaretFromItsAnchor(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("abcdef"), "Home", "Right", "Shift-Right", "Shift-Right"]);

        Assert.Equal("bc", Selected(surface, typed));
        Assert.Equal("bcdef", Selected(surface, Keys(typed, "Shift-End")));
        Assert.Equal("a", Selected(surface, Keys(typed, "Left", "Shift-Home")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenCtrlShiftLeft_WhenPressed_ThenTheWordIsSelected(string surface)
    {
        var typed = Keys(Open(surface), [.. Type(Words)]);

        Assert.Equal("gamma", Selected(surface, Keys(typed, "Ctrl-Shift-Left")));
        Assert.Equal("beta gamma", Selected(surface, Keys(typed, "Ctrl-Shift-Left", "Ctrl-Shift-Left")));
        Assert.Equal("alpha", Selected(surface, Keys(typed, "Home", "Ctrl-Shift-Right")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAltA_WhenPressed_ThenAllTheTextIsSelectedAndTypingReplacesIt(string surface)
    {
        var selected = Keys(Open(surface), [.. Type(Words), "Left", "Alt-a"]);

        Assert.Equal(Words, Selected(surface, selected));
        Assert.Equal(("X", 1), Read(surface, Keys(selected, "X")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASelection_WhenTyping_ThenTheTypedCharacterReplacesIt(string surface)
    {
        var state = Keys(Open(surface), [.. Type(Words), "Ctrl-Shift-Left", "X"]);

        Assert.Equal(("alpha beta X", 12), Read(surface, state));
        Assert.Equal(string.Empty, Selected(surface, state));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASelection_WhenBackspaceOrDelete_ThenOnlyTheSelectionGoes(string surface)
    {
        var selected = Keys(Open(surface), [.. Type(Words), "Ctrl-Shift-Left"]);

        Assert.Equal(("alpha beta ", 11), Read(surface, Keys(selected, "Backspace")));
        Assert.Equal(("alpha beta ", 11), Read(surface, Keys(selected, "Delete")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASelection_WhenMovingLeftOrRight_ThenItCollapsesToItsStartOrEnd(string surface)
    {
        var selected = Keys(Open(surface), [.. Type("abcdef"), "Shift-Left", "Shift-Left"]);

        Assert.Equal(("abcdef", 4), Read(surface, Keys(selected, "Left")));
        Assert.Equal(("abcdef", 6), Read(surface, Keys(selected, "Right")));
        Assert.Equal(string.Empty, Selected(surface, Keys(selected, "Left")));
    }

    [Theory]
    [MemberData(nameof(LineSurfaces))]
    public void GivenSeveralLines_WhenSelectingUpAndDown_ThenTheSelectionSpansTheLines(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("one"), "Enter", .. Type("two"), "Enter", .. Type("three")]);

        var up = Keys(typed, "Shift-Up");

        Assert.Equal("\nthree", Selected(surface, up));
        Assert.Equal("wo\nthree", Selected(surface, Keys(up, "Shift-Left", "Shift-Left")));
        Assert.Equal("ee", Selected(surface, Keys(up, "Shift-Down")));
    }

    [Fact]
    public void GivenTheSummaryField_WhenShiftUpIsPressed_ThenNothingIsSelectedAndTheTextStays()
    {
        var state = Keys(Open("form-text"), [.. Type("abc"), "Shift-Up"]);

        Assert.Equal(("abc", 3), Read("form-text", state));
        Assert.Equal(string.Empty, Selected("form-text", state));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASelection_WhenCopying_ThenTheClipboardGetsItAndTheTextAndSelectionStay(string surface)
    {
        var selected = Keys(Open(surface), [.. Type(Words), "Ctrl-Shift-Left"]);

        var result = _reducer.Reduce(selected, new KeyPressed("Alt-w", _now));

        var copy = Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects));
        Assert.Equal("gamma", copy.Text);
        Assert.Equal((Words, 11), Read(surface, result.State));
        Assert.Equal("gamma", Selected(surface, result.State));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASelection_WhenCutting_ThenTheClipboardGetsItAndItIsRemoved(string surface)
    {
        var selected = Keys(Open(surface), [.. Type(Words), "Ctrl-Shift-Left"]);

        var result = _reducer.Reduce(selected, new KeyPressed("Alt-x", _now));

        Assert.Equal("gamma", Assert.IsType<CopyToClipboardEffect>(Assert.Single(result.Effects)).Text);
        Assert.Equal(("alpha beta ", 11), Read(surface, result.State));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenNoSelection_WhenCopyingOrCutting_ThenNothingHappens(string surface)
    {
        var typed = Keys(Open(surface), [.. Type(Words)]);

        var copied = _reducer.Reduce(typed, new KeyPressed("Alt-w", _now));
        var cut = _reducer.Reduce(typed, new KeyPressed("Alt-x", _now));

        Assert.Empty(copied.Effects);
        Assert.Empty(cut.Effects);
        Assert.Equal((Words, 16), Read(surface, cut.State));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenASelection_WhenDrawn_ThenItIsInTheSelectionRoleAndTheCaretCellCoversItsMovingEnd(string surface)
    {
        var state = Keys(Open(surface), [.. Type(Words), "Left", "Left", "Ctrl-Shift-Left"]);

        var spans = Frame(state).Body.SelectMany(static line => line.Spans).ToList();

        var selected = spans.Where(static span => span.Role == CaretText.SelectedRole);

        Assert.Equal("am", string.Concat(selected.Select(static span => span.Text)));
        Assert.Equal("g", Assert.Single(spans, static span => span.Role == TextRole.EditCaret).Text);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenNoSelection_WhenDrawn_ThenNothingIsInTheSelectionRole(string surface)
    {
        var state = Keys(Open(surface), [.. Type(Words), "Left"]);

        var spans = Frame(state).Body.SelectMany(static line => line.Spans).ToList();

        Assert.DoesNotContain(spans, static span => span.Role == CaretText.SelectedRole);
    }

    private static string[] Type(string text)
    {
        return [.. text.Select(static character => character == ' ' ? "Space" : character.ToString())];
    }

    private static StyledFrame Frame(AppState state)
    {
        return FrameBuilder.BuildStyled(state, 100, 30, _keymap)!;
    }

    private static AppState Open(string surface)
    {
        return TextEditorContractTests.Open(surface);
    }

    private static (string Text, int Cursor) Read(string surface, AppState state)
    {
        if (surface == "comment")
        {
            return (state.Editor!.Buffer.Text, state.Editor.Buffer.Cursor);
        }

        var field = state.Session.Tabs[0].Form!.Fields[0];

        return (field.Value.Text, field.Value.Text.Length - field.Back);
    }

    private static string Selected(string surface, AppState state)
    {
        return surface == "comment"
            ? state.Editor!.Buffer.SelectedText
            : FormEditor.SelectedText(state.Session.Tabs[0].Form!.Fields[0]);
    }

    private AppState Keys(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key, _now)).State);
    }
}
