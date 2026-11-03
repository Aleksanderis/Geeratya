using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Core.Tests.State;

[Trait("Os", "Independent")]
public class InputRoutingTests
{
    private readonly KeymapTable _keymap = DefaultKeymap.Load();
    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Theory]
    [InlineData("q")]
    [InlineData(":")]
    [InlineData("g")]
    [InlineData("1")]
    public void GivenHelpOpen_WhenAnyKeyArrives_ThenTheBodyNeverGetsIt(string key)
    {
        var help = Press(AppState.Initial, "?").State;

        var result = Press(help, key);

        Assert.NotEqual(InputMode.Command, result.State.Mode);
        Assert.Empty(result.Effects);
        Assert.Equal(0, result.State.Count);
        Assert.Equal(string.Empty, result.State.PendingKeys);
    }

    [Fact]
    public void GivenCommandMode_WhenTypingChordKeys_ThenTheyAreTextAndNeverFireChords()
    {
        var state = Press(AppState.Initial, ":").State;

        var route = InputRouting.Route(state, "g", _keymap);
        var typed = Press(Press(state, "g").State, "g").State;

        Assert.Equal(KeyRouteKind.Text, route.Kind);
        Assert.Equal("gg", typed.PaletteText);
        Assert.Equal(string.Empty, typed.PendingKeys);
    }

    [Fact]
    public void GivenCommandMode_WhenTypingQ_ThenItIsTextAndDoesNotQuit()
    {
        var state = Press(AppState.Initial, ":").State;

        var result = Press(state, "q");

        Assert.Empty(result.Effects);
        Assert.Equal("q", result.State.PaletteText);
    }

    [Fact]
    public void GivenAHalfTypedChord_WhenPressingEsc_ThenOnlyTheChordIsDropped()
    {
        var pending = Press(AppState.Initial, "g").State;

        var result = Press(pending, "Esc");

        Assert.Equal(string.Empty, result.State.PendingKeys);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenCommandMode_WhenPressingEsc_ThenItBacksOutToNormalAndKeepsRunning()
    {
        var state = Press(AppState.Initial, ":").State;

        var result = Press(state, "Esc");

        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenANoticeShowing_WhenPressingEsc_ThenItIsDismissedBeforeAnythingElse()
    {
        var state = StatusMessages.Warn(AppState.Initial, "Something happened");

        var route = InputRouting.Route(state, "Esc", _keymap);
        var result = Press(state, "Esc");

        Assert.Equal(KeyRouteKind.Dismiss, route.Kind);
        Assert.Null(result.State.Status);
    }

    [Fact]
    public void GivenNormalModeWithNothingToBackOutOf_WhenPressingEsc_ThenNothingHappens()
    {
        var result = Press(AppState.Initial, "Esc");

        Assert.Equal(AppState.Initial, result.State);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenNormalMode_WhenTypingADigit_ThenItStartsACount()
    {
        Assert.Equal(KeyRouteKind.Count, InputRouting.Route(AppState.Initial, "3", _keymap).Kind);
    }

    [Fact]
    public void GivenCommandMode_WhenTypingADigit_ThenItIsTextNotACount()
    {
        var command = Press(AppState.Initial, ":").State;

        Assert.Equal(KeyRouteKind.Text, InputRouting.Route(command, "3", _keymap).Kind);
    }

    [Fact]
    public void GivenNormalMode_WhenTypingAChordPrefix_ThenItIsHeldBackUntilTheChordCompletes()
    {
        Assert.Equal(KeyRouteKind.ChordPrefix, InputRouting.Route(AppState.Initial, "g", _keymap).Kind);
    }

    [Fact]
    public void GivenNormalMode_WhenTypingQ_ThenItRoutesToTheQuitAction()
    {
        var route = InputRouting.Route(AppState.Initial, "q", _keymap);

        Assert.Equal(KeyRouteKind.Action, route.Kind);
        Assert.Equal(ActionIds.Quit, route.Resolution!.Action);
    }

    [Fact]
    public void GivenTheSameAction_WhenRequestedByKeyAndByGesture_ThenTheStatesMatch()
    {
        var byKey = Press(AppState.Initial, "?");
        var byGesture = _reducer.Reduce(AppState.Initial, new ActionRequested(ActionIds.HelpToggle));

        Assert.Equal(byKey.State, byGesture.State);
        Assert.Equal(byKey.Effects, byGesture.Effects);
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }
}
