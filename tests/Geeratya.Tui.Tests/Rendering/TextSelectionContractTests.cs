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

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenTypedWords_WhenUndoAndRedo_ThenTheyGoOneWordAtATime(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("one two")]);

        var undone = Keys(typed, "Ctrl-z");

        Assert.Equal(("one ", 4), Read(surface, undone));
        Assert.Equal(("one", 3), Read(surface, Keys(undone, "Ctrl-z")));
        Assert.Equal(("one ", 4), Read(surface, Keys(undone, "Ctrl-z", "Ctrl-y")));
        Assert.Equal(("one two", 7), Read(surface, Keys(undone, "Ctrl-y")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenAReplacedSelection_WhenUndone_ThenTheTextAndTheSelectionAreBack(string surface)
    {
        var selected = Keys(Open(surface), [.. Type(Words), "Ctrl-Shift-Left"]);

        var replaced = Keys(selected, "X");
        var undone = Keys(replaced, "Ctrl-z");

        Assert.Equal(("alpha beta X", 12), Read(surface, replaced));
        Assert.Equal((Words, 11), Read(surface, undone));
        Assert.Equal("gamma", Selected(surface, undone));
        Assert.Equal(("alpha beta X", 12), Read(surface, Keys(undone, "Ctrl-y")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenDeletes_WhenUndone_ThenARunOfBackspacesIsOneStepAndACutIsAnother(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("abcdef")]);
        var backspaced = Keys(typed, "Backspace", "Backspace");

        Assert.Equal(("abcd", 4), Read(surface, backspaced));
        Assert.Equal(("abcdef", 6), Read(surface, Keys(backspaced, "Ctrl-z")));

        var cut = _reducer.Reduce(Keys(typed, "Shift-Left", "Shift-Left"), new KeyPressed("Alt-x", _now)).State;

        Assert.Equal(("abcd", 4), Read(surface, cut));
        Assert.Equal(("abcdef", 4), Read(surface, Keys(cut, "Ctrl-z")));
        Assert.Equal("ef", Selected(surface, Keys(cut, "Ctrl-z")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenACursorMove_WhenTypingAgain_ThenItIsANewStepAndANewEditClearsRedo(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("ab"), "Left", "X"]);

        Assert.Equal(("aXb", 2), Read(surface, typed));
        Assert.Equal(("ab", 1), Read(surface, Keys(typed, "Ctrl-z")));

        var edited = Keys(typed, "Ctrl-z", "Ctrl-z", "Y");

        Assert.Equal(("Y", 1), Read(surface, edited));
        Assert.Equal(("Y", 1), Read(surface, Keys(edited, "Ctrl-y")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenACursorMoveAndReturn_WhenTypingAgain_ThenTheEarlierRunIsNotJoined(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("ab"), "Left", "Right", "c"]);

        Assert.Equal(("abc", 3), Read(surface, typed));
        Assert.Equal(("ab", 2), Read(surface, Keys(typed, "Ctrl-z")));
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void GivenNothingToUndoOrRedo_WhenPressed_ThenTheTextStays(string surface)
    {
        var opened = Open(surface);
        var typed = Keys(opened, [.. Type("abc")]);

        Assert.Equal((string.Empty, 0), Read(surface, Keys(opened, "Ctrl-z", "Ctrl-y")));
        Assert.Equal(("abc", 3), Read(surface, Keys(typed, "Ctrl-y")));
        Assert.Equal((string.Empty, 0), Read(surface, Keys(typed, "Ctrl-z", "Ctrl-z", "Ctrl-z")));
    }

    [Theory]
    [MemberData(nameof(LineSurfaces))]
    public void GivenALineBreak_WhenUndone_ThenItIsAStepOfItsOwn(string surface)
    {
        var typed = Keys(Open(surface), [.. Type("one"), "Enter", .. Type("two")]);

        Assert.Equal(("one\n", 4), Read(surface, Keys(typed, "Ctrl-z")));
        Assert.Equal(("one", 3), Read(surface, Keys(typed, "Ctrl-z", "Ctrl-z")));
    }

    [Fact]
    public void GivenThePalette_WhenCtrlZIsPressed_ThenTheTypedTextStays()
    {
        var typed = Keys(TextEditorContractTests.Open("palette"), [.. Type("rank")]);

        Assert.Equal(typed.PaletteBack, Keys(typed, "Ctrl-z", "Ctrl-y").PaletteBack);
        Assert.Equal(typed.PaletteText, Keys(typed, "Ctrl-z").PaletteText);
    }

    [Fact]
    public void GivenThePalette_WhenShiftDownOrShiftLeftIsPressed_ThenTheHighlightAndTheCaretMoveAsWithoutShift()
    {
        var typed = Keys(TextEditorContractTests.Open("palette"), [.. Type("ra")]);
        var plain = Keys(typed, "Down", "Left");
        var shifted = Keys(typed, "Shift-Down", "Shift-Left");

        Assert.Equal(plain.PalettePick, shifted.PalettePick);
        Assert.Equal(plain.PaletteBack, shifted.PaletteBack);
        Assert.NotEqual(typed.PalettePick, shifted.PalettePick);
        Assert.NotEqual(typed.PaletteBack, shifted.PaletteBack);
    }

    [Fact]
    public void GivenThePalette_WhenAltAIsPressed_ThenTheCaretStaysWhereItIs()
    {
        var typed = Keys(TextEditorContractTests.Open("palette"), [.. Type("rank"), "Left"]);

        var selected = Keys(typed, "Alt-a");

        Assert.Equal(typed.PaletteBack, selected.PaletteBack);
    }

    [Fact]
    public void GivenTheSummaryField_WhenShiftDownIsPressed_ThenItActsAsDownAndSelectsNothing()
    {
        var typed = Keys(Open("form-text"), [.. Type("abc")]);

        var shifted = Keys(typed, "Shift-Down");

        Assert.Equal(Keys(typed, "Down").Session.Tabs[0].Form!.Focus, shifted.Session.Tabs[0].Form!.Focus);
        Assert.Equal(string.Empty, Selected("form-text", shifted));
    }

    [Fact]
    public void GivenALabelsEntry_WhenShiftLeftIsPressed_ThenTheCaretMovesLikeLeftAndNothingIsSelected()
    {
        var labels = FormField.Create("labels", "Labels", FormFieldKind.Labels, false, []);
        var typed = Keys(TextEditorContractTests.OpenForm(labels), [.. Type("abc")]);

        var shifted = Keys(typed, "Shift-Left", "Shift-Left");

        var field = shifted.Session.Tabs[0].Form!.Fields[0];
        Assert.Equal(2, field.Back);
        Assert.Null(field.AnchorBack);
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
