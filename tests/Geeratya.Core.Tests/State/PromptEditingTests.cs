using Geeratya.Core.Jump;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

/// <summary>The caret and editing keys of the palette, the source picker, the jump prompt and the help filter.</summary>
[Trait("Os", "Independent")]
public class PromptEditingTests
{
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenThePalette_WhenPressingLeftAndTyping_ThenTheCharacterGoesInAtTheCaret()
    {
        var state = Press(InPalette(), "a", "b", "c", "Left", "X");

        Assert.Equal("abXc", state.PaletteText);
    }

    [Fact]
    public void GivenThePalette_WhenPressingCtrlWAfterTwoWords_ThenTheLastWordGoes()
    {
        var state = Press(InPalette(), "o", "p", "e", "n", "Space", "b", "o", "a", "r", "d", "Ctrl-w");

        Assert.Equal("open ", state.PaletteText);
    }

    [Fact]
    public void GivenThePaletteWithAMovedCaret_WhenTheTextChanges_ThenTheHighlightGoesBackToTheFirstRow()
    {
        var state = InPalette() with { PaletteText = "ab", PaletteSelected = 3 };

        var moved = Press(state, "Left");
        var typed = Press(moved, "X");

        Assert.Equal(3, moved.PaletteSelected);
        Assert.Equal(0, typed.PaletteSelected);
    }

    [Fact]
    public void GivenThePalette_WhenUsingHomeEndAndDelete_ThenTheCaretMovesAndTheCharacterUnderItGoes()
    {
        var state = Press(InPalette(), "a", "b", "c", "Home", "Delete", "Ctrl-e", "d");

        Assert.Equal("bcd", state.PaletteText);
    }

    [Fact]
    public void GivenThePalette_WhenPressingCtrlU_ThenTheTextBeforeTheCaretGoes()
    {
        var state = Press(InPalette(), "a", "b", "c", "d", "Left", "Left", "Ctrl-u");

        Assert.Equal("cd", state.PaletteText);
    }

    [Fact]
    public void GivenAnEmptyPaletteLine_WhenBackspacing_ThenThePaletteCloses()
    {
        var state = Press(InPalette(), "Backspace");

        Assert.Equal(InputMode.Normal, state.Mode);
    }

    [Fact]
    public void GivenThePalette_WhenAltBMovesAWordBack_ThenBackspaceDeletesBeforeTheCaret()
    {
        var state = Press(InPalette(), "a", "a", "Space", "b", "b", "Alt-b", "Backspace");

        Assert.Equal("aabb", state.PaletteText);
    }

    [Fact]
    public void GivenThePicker_WhenPressingLeftTypingAndDeletingAWord_ThenItEditsAtTheCaret()
    {
        var state = Press(InPicker(), "a", "b", "c", "Left", "X");

        Assert.Equal("abXc", state.Picker!.Text);

        var deleted = Press(state, "Ctrl-w", "Y");

        Assert.Equal("Yc", deleted.Picker!.Text);
    }

    [Fact]
    public void GivenThePicker_WhenTheCaretMoves_ThenTheHighlightAndNoticeStay()
    {
        var state = InPicker() with { Picker = new SourcePickerState("ab", 2, PickerScope.All, false, "No match") };

        var moved = Press(state, "Left");

        Assert.Equal(2, moved.Picker!.Selected);
        Assert.Equal("No match", moved.Picker.Notice);
        Assert.Equal(1, moved.Picker.Back);
    }

    [Fact]
    public void GivenThePicker_WhenCtrlJAndCtrlKAreUsed_ThenTheyStayPickerNavigationAndTheTextIsKept()
    {
        var state = Press(InPicker(), "a", "b", "Ctrl-k", "Ctrl-j");

        Assert.Equal("ab", state.Picker!.Text);
    }

    [Fact]
    public void GivenTheJumpPrompt_WhenPressingLeftAndTyping_ThenTheCharacterGoesInAtTheCaret()
    {
        var state = Press(InJump(), "A", "-", "1", "Left", "2");

        Assert.Equal("A-21", state.Jump!.Text);
    }

    [Fact]
    public void GivenTheJumpPrompt_WhenPressingCtrlWAndHome_ThenTheWordBeforeTheCaretGoes()
    {
        var state = Press(InJump(), "A", "-", "1", "2", "Ctrl-w");

        Assert.Equal("A-", state.Jump!.Text);
        Assert.Equal("1A-", Press(state, "Home", "1").Jump!.Text);
    }

    [Fact]
    public void GivenTheHelpFilter_WhenPressingLeftAndTyping_ThenTheCharacterGoesInAtTheCaret()
    {
        var state = Press(AppState.Initial, "?", "/", "a", "b", "c", "Left", "X");

        Assert.Equal("abXc", state.HelpFilter);
    }

    [Fact]
    public void GivenTheHelpFilter_WhenPressingCtrlU_ThenItDeletesToTheStartInsteadOfScrollingAPage()
    {
        var state = Press(AppState.Initial, "?", "/", "a", "b", "Left", "Ctrl-u");

        Assert.Equal("b", state.HelpFilter);
        Assert.True(state.HelpFiltering);
    }

    [Fact]
    public void GivenTheHelpFilter_WhenClearedWithEsc_ThenTheNextFilterStartsWithTheCaretAtTheEnd()
    {
        var state = Press(AppState.Initial, "?", "/", "a", "b", "Left", "Esc", "/", "x", "y");

        Assert.Equal("xy", state.HelpFilter);
    }

    private AppState Press(AppState state, params string[] keys)
    {
        return keys.Aggregate(state, (current, key) => _reducer.Reduce(current, new KeyPressed(key)).State);
    }

    private static AppState InPalette()
    {
        return AppState.Initial with { Mode = InputMode.Command };
    }

    private static AppState InPicker()
    {
        var picker = new SourcePickerState(string.Empty, 0, PickerScope.All, false, null);

        return AppState.Initial with { Mode = InputMode.Insert, Picker = picker };
    }

    private static AppState InJump()
    {
        return AppState.Initial with { Mode = InputMode.Insert, Jump = new JumpState(string.Empty, 0, null) };
    }
}
