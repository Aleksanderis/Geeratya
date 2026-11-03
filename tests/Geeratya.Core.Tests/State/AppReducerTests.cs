using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class AppReducerTests
{
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenNormalMode_WhenPressingQ_ThenQuitEffect()
    {
        var result = Press(AppState.Initial, "q");

        Assert.Single(result.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenNormalMode_WhenPressingQuestionMark_ThenHelpOpensAndQuestionMarkClosesIt()
    {
        var open = Press(AppState.Initial, "?");
        var closed = Press(open.State, "?");

        Assert.Equal(InputMode.Help, open.State.Mode);
        Assert.Equal(InputMode.Normal, closed.State.Mode);
    }

    [Fact]
    public void GivenHelpOpen_WhenPressingQ_ThenHelpClosesAndAppKeepsRunning()
    {
        var help = Press(AppState.Initial, "?").State;

        var result = Press(help, "q");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenHelpOpen_WhenPressingEsc_ThenHelpCloses()
    {
        var help = Press(AppState.Initial, "?").State;

        Assert.Equal(InputMode.Normal, Press(help, "Esc").State.Mode);
    }

    [Fact]
    public void GivenHelpOpen_WhenScrollingPastTheTopAndDown_ThenScrollStaysInRange()
    {
        var help = Press(AppState.Initial, "?").State;

        var top = Press(help, "k").State;
        var down = Press(help, "j").State;

        Assert.Equal(0, top.HelpScroll);
        Assert.Equal(1, down.HelpScroll);
    }

    [Fact]
    public void GivenHelpOpen_WhenScrollingFarDown_ThenScrollStopsAtLastLine()
    {
        var state = Press(AppState.Initial, "?").State;
        var lineCount = KeymapDocument.RenderHelpLines(DefaultKeymap.Load()).Count;

        for (var step = 0; step < lineCount + 10; step++)
        {
            state = Press(state, "j").State;
        }

        Assert.Equal(lineCount - 1, state.HelpScroll);
    }

    [Fact]
    public void GivenNormalMode_WhenPressingColon_ThenCommandModeAndKeysAreText()
    {
        var state = Press(AppState.Initial, ":").State;

        state = Press(state, "q").State;
        state = Press(state, "Space").State;
        state = Press(state, "j").State;

        Assert.Equal(InputMode.Command, state.Mode);
        Assert.Equal("q j", state.PaletteText);
    }

    [Fact]
    public void GivenCommandMode_WhenTypingQuitAndEnter_ThenQuitEffect()
    {
        var state = TypeCommand("quit");

        var result = Press(state, "Enter");

        Assert.Single(result.Effects.OfType<QuitEffect>());
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenCommandMode_WhenTypingUnknownAndEnter_ThenMessageNamesIt()
    {
        var result = Press(TypeCommand("zzqx"), "Enter");

        Assert.Equal("Unknown command: zzqx", result.State.Message);
        Assert.Equal(string.Empty, result.State.PaletteText);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenCommandMode_WhenBackspaceOnEmptyText_ThenReturnsToNormal()
    {
        var state = Press(AppState.Initial, ":").State;

        Assert.Equal(InputMode.Normal, Press(state, "Backspace").State.Mode);
    }

    [Fact]
    public void GivenCommandMode_WhenBackspace_ThenRemovesLastCharacter()
    {
        var state = Press(TypeCommand("ab"), "Backspace").State;

        Assert.Equal("a", state.PaletteText);
        Assert.Equal(InputMode.Command, state.Mode);
    }

    [Fact]
    public void GivenCommandModeWithEmoji_WhenBackspace_ThenRemovesWholeCharacter()
    {
        var state = Press(Press(AppState.Initial, ":").State, "😀").State;

        Assert.Equal(string.Empty, Press(state, "Backspace").State.PaletteText);
    }

    [Theory]
    [InlineData(InputMode.Normal)]
    [InlineData(InputMode.Help)]
    [InlineData(InputMode.Command)]
    public void GivenNormalHelpOrCommandMode_WhenPressingCtrlC_ThenQuitEffect(InputMode mode)
    {
        var result = Press(AppState.Initial with { Mode = mode }, "Ctrl-c");

        Assert.Single(result.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenCommandMode_WhenEsc_ThenCancelsWithoutRunning()
    {
        var result = Press(TypeCommand("quit"), "Esc");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Equal(string.Empty, result.State.PaletteText);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenCommandMode_WhenCtrlCombination_ThenItIsNotText()
    {
        var state = Press(Press(AppState.Initial, ":").State, "Ctrl-d").State;

        Assert.Equal(string.Empty, state.PaletteText);
    }

    [Fact]
    public void GivenNormalMode_WhenTypingGg_ThenPendingThenCleared()
    {
        var first = Press(AppState.Initial, "g").State;
        var second = Press(first, "g").State;

        Assert.Equal("g", first.PendingKeys);
        Assert.Equal(string.Empty, second.PendingKeys);
    }

    [Fact]
    public void GivenPendingKey_WhenPressingUnboundKey_ThenPendingIsDropped()
    {
        var state = Press(Press(AppState.Initial, "g").State, "x").State;

        Assert.Equal(string.Empty, state.PendingKeys);
    }

    [Fact]
    public void GivenMessage_WhenPressingEsc_ThenMessageClearedAndAppKeepsRunning()
    {
        var withMessage = AppState.Initial with { Message = "Unknown command: x" };

        var result = Press(withMessage, "Esc");

        Assert.Null(result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenConfirmMode_WhenPressingCtrlC_ThenQuitsAtOnce()
    {
        var result = Press(Confirming(ActionIds.IssueTransition), "Ctrl-c");

        Assert.Single(result.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenInsertMode_WhenPressingCtrlC_ThenItCancelsAndNeverQuits()
    {
        var result = Press(AppState.Initial with { Mode = InputMode.Insert }, "Ctrl-c");

        Assert.Empty(result.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenConfirmMode_WhenPressingQEnterOrOtherKeys_ThenNothingHappens()
    {
        var state = Confirming(ActionIds.Quit);

        foreach (var key in new[] { "q", "Enter", "x", ":" })
        {
            var result = Press(state, key);

            Assert.Equal(InputMode.Confirm, result.State.Mode);
            Assert.Empty(result.Effects);
        }
    }

    [Fact]
    public void GivenConfirmMode_WhenPressingY_ThenRunsTheConfirmedAction()
    {
        var result = Press(Confirming(ActionIds.Quit), "y");

        Assert.Single(result.Effects.OfType<QuitEffect>());
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Theory]
    [InlineData("n")]
    [InlineData("Esc")]
    public void GivenConfirmMode_WhenDeclining_ThenNothingRunsAndModeIsNormal(string key)
    {
        var result = Press(Confirming(ActionIds.Quit), key);

        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(result.State.Confirm);
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private AppState TypeCommand(string text)
    {
        var state = Press(AppState.Initial, ":").State;

        foreach (var character in text)
        {
            state = Press(state, character.ToString()).State;
        }

        return state;
    }

    private AppState Confirming(string action)
    {
        return _reducer.Reduce(AppState.Initial, new ConfirmRequested(new ConfirmRequest("Do it?", action))).State;
    }
}
