using Geeratya.Core.Comments;
using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;

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

    [Theory]
    [InlineData("q")]
    [InlineData("g")]
    [InlineData(":")]
    public void GivenTheProfileForm_WhenAnyKeyArrives_ThenItIsTextAndNoBodyActionRuns(string key)
    {
        var form = TypeAndSubmit("profile add");

        var route = InputRouting.Route(form, key, _keymap);
        var result = Press(form, key);

        Assert.Equal(KeyRouteKind.Text, route.Kind);
        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Theory]
    [InlineData("q")]
    [InlineData("g")]
    [InlineData(":")]
    [InlineData("Enter")]
    public void GivenConfirmOpen_WhenAnyOtherKeyArrives_ThenItIsSwallowedAndTheDialogStays(string key)
    {
        var confirm = _reducer.Reduce(
            AppState.Initial,
            new ConfirmRequested(new ConfirmRequest("Do it?", ActionIds.Quit))).State;

        var route = InputRouting.Route(confirm, key, _keymap);
        var result = Press(confirm, key);

        Assert.NotEqual(KeyRouteKind.Count, route.Kind);
        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.NotNull(result.State.Confirm);
    }

    [Fact]
    public void GivenTheEditorWithAPopup_WhenPressingEsc_ThenThePopupClosesBeforeTheEditor()
    {
        var editing = Editing();
        var typed = Press(Press(Press(editing, "h").State, KeyNotation.Space).State, "@").State;
        var route = InputRouting.Route(typed, "Esc", _keymap);

        var result = Press(typed, "Esc");

        Assert.Equal(KeyRouteKind.SuggestionPopup, route.Kind);
        Assert.NotNull(result.State.Editor);
        Assert.Null(result.State.Editor!.Suggest);
    }

    [Fact]
    public void GivenTheEditorWithoutAPopup_WhenTypingAKey_ThenItIsTextForTheEditor()
    {
        var route = InputRouting.Route(Editing(), "x", _keymap);

        Assert.Equal(KeyRouteKind.Text, route.Kind);
    }

    [Fact]
    public void GivenTheSameAction_WhenRunFromThePaletteAndByKey_ThenTheOutcomeMatches()
    {
        var byKey = Press(AppState.Initial, "q");
        var byPalette = Press(TypeAndSubmitState("quit"), "Enter");
        var byGesture = _reducer.Reduce(AppState.Initial, new ActionRequested(ActionIds.Quit));

        Assert.Single(byKey.Effects.OfType<QuitEffect>());
        Assert.Single(byPalette.Effects.OfType<QuitEffect>());
        Assert.Single(byGesture.Effects.OfType<QuitEffect>());
    }

    private AppState TypeAndSubmit(string text)
    {
        return Press(TypeAndSubmitState(text), "Enter").State;
    }

    private AppState TypeAndSubmitState(string text)
    {
        var state = Press(AppState.Initial, ":").State;

        foreach (var character in text)
        {
            state = Press(state, character == ' ' ? KeyNotation.Space : character.ToString()).State;
        }

        return state;
    }

    private static AppState Editing()
    {
        var profiles = ProfileCatalog.Empty.With(new Profile("work", "https://work.atlassian.net", "me@work.test"));
        var editor = new CommentEditor(new TabId(1), "A-1", null, TextBuffer.Empty, string.Empty);

        return AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active),
            Mode = InputMode.Insert,
            Editor = editor,
        };
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }
}
