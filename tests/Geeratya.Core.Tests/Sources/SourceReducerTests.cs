using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Filters.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Sources;

[Trait("Os", "Independent")]
public class SourceReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardSummary[] _boards =
    [
        new(4, "GCM board", BoardType.Kanban, "GCM"),
        new(3, "GTM board", BoardType.Simple, "GTM"),
        new(7, "Growth board", BoardType.Scrum, string.Empty),
    ];

    private static readonly SavedFilter[] _filters =
    [
        new("10001", "Mine", null, "Me", true, null),
        new("10002", "Minefield", null, "Me", false, null),
        new("10003", "Filter for GCM board", null, "Me", false, null),
    ];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenNoSession_WhenPressingSpaceT_ThenThePickerOpensInInsertModeAndAsksForBothLists()
    {
        var result = Press(Press(Start(), "Space").State, "t");

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(PickerScope.All, result.State.Picker!.Scope);
        var effect = Assert.Single(result.Effects.OfType<LoadSourcesEffect>());
        Assert.True(effect.Boards);
        Assert.True(effect.Filters);
        Assert.True(result.State.Session.Sources.BoardsLoading);
        Assert.True(result.State.Session.Sources.FiltersLoading);
    }

    [Fact]
    public void GivenNoProfile_WhenOpeningThePickerOrACommand_ThenTheMessageSaysToAddOne()
    {
        var picker = Press(Press(AppState.Initial, "Space").State, "t");
        var board = Run(AppState.Initial, "board gcm");

        Assert.Null(picker.State.Picker);
        Assert.Equal("No profile yet: add one with :profile add.", picker.State.Message);
        Assert.Null(board.State.Picker);
        Assert.Equal("No profile yet: add one with :profile add.", board.State.Message);
    }

    [Fact]
    public void GivenListsAreLoaded_WhenOpeningThePickerAgain_ThenNothingIsRequestedAgain()
    {
        var state = Loaded(Start());

        var result = Press(Press(state, "Space").State, "t");

        Assert.DoesNotContain(result.Effects, effect => effect is LoadSourcesEffect);
        Assert.NotNull(result.State.Picker);
    }

    [Fact]
    public void GivenAListIsInFlight_WhenOpeningThePickerAgain_ThenOnlyTheOtherListIsRequested()
    {
        var first = Press(Press(Start(), "Space").State, "t");
        var cancelled = Press(first.State, "Esc");
        var boards = _reducer.Reduce(cancelled.State, new BoardsLoaded(_boards));

        var second = Press(Press(boards.State, "Space").State, "t");

        Assert.DoesNotContain(second.Effects, effect => effect is LoadSourcesEffect);
        Assert.True(second.State.Session.Sources.FiltersLoading);
    }

    [Fact]
    public void GivenAFailedLoad_WhenOpeningThePickerAgain_ThenItIsRetried()
    {
        var opened = Press(Press(Start(), "Space").State, "t");
        var failed = _reducer.Reduce(opened.State, Failure(SourceListKind.Boards, JiraErrorKind.Forbidden));
        var closed = Press(failed.State, "Esc");

        var again = Press(Press(closed.State, "Space").State, "t");

        var effect = Assert.Single(again.Effects.OfType<LoadSourcesEffect>());
        Assert.True(effect.Boards);
        Assert.False(effect.Filters);
        Assert.Null(again.State.Session.Sources.BoardsError);
    }

    [Fact]
    public void GivenAFailedBoardsLoad_WhenItIsReported_ThenTheErrorIsKeptAndNotCachedAsLoaded()
    {
        var opened = Press(Press(Start(), "Space").State, "t");

        var failed = _reducer.Reduce(opened.State, Failure(SourceListKind.Boards, JiraErrorKind.NotFound));

        var lists = failed.State.Session.Sources;
        Assert.Equal("Not found, or you have no access to it.", lists.BoardsError);
        Assert.False(lists.BoardsLoading);
        Assert.False(lists.IsLoaded(SourceListKind.Boards));
    }

    [Fact]
    public void GivenBoardCommandWithUniqueName_WhenListsAreLoaded_ThenTheBoardOpensAsColumnsInANewTab()
    {
        var result = Run(Loaded(Start()), "board GCM");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "GCM board"), tab.Definition);
        Assert.Null(result.State.Picker);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.IsType<LoadViewPageEffect>(Assert.Single(result.Effects));
    }

    [Fact]
    public void GivenBacklogCommandWithPrefix_WhenListsAreLoaded_ThenTheBacklogOpensAsList()
    {
        var result = Run(Loaded(Start()), "backlog gtm");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.Backlog, "3", ViewLayout.List, "GTM board"), tab.Definition);
    }

    [Fact]
    public void GivenFilterCommandWithExactName_WhenAnotherFilterSharesThePrefix_ThenTheExactOneWins()
    {
        var result = Run(Loaded(Start()), "filter mine");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.SavedFilter, "10001", ViewLayout.List, "Mine"), tab.Definition);
    }

    [Fact]
    public void GivenQuotedName_WhenRunningBoard_ThenTheQuotesAreIgnored()
    {
        var result = Run(Loaded(Start()), "board \"gcm board\"");

        Assert.Equal("4", Assert.Single(result.State.Session.Tabs).Definition.SourceValue);
    }

    [Fact]
    public void GivenAmbiguousPrefix_WhenRunningBoard_ThenThePickerStaysOpenWithTheTextAndANotice()
    {
        var result = Run(Loaded(Start()), "board g");

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal("g", result.State.Picker!.Text);
        Assert.Equal(PickerScope.Boards, result.State.Picker.Scope);
        Assert.Equal("\"g\" matches 3 boards: choose one.", result.State.Picker.Notice);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenUnknownName_WhenRunningFilter_ThenThePickerOpensFilteredWithANotice()
    {
        var result = Run(Loaded(Start()), "filter zzz");

        Assert.Empty(result.State.Session.Tabs);
        Assert.Equal("No filter named \"zzz\".", result.State.Picker!.Notice);
        Assert.Equal(PickerScope.Filters, result.State.Picker.Scope);
        Assert.Empty(SourceChoices.For(result.State.Picker, result.State.Session.Sources));
    }

    [Fact]
    public void GivenSeveralBoardsAndNoName_WhenRunningBoard_ThenThePickerOpensWithoutNotice()
    {
        var result = Run(Loaded(Start()), "board");

        Assert.Equal(string.Empty, result.State.Picker!.Text);
        Assert.Null(result.State.Picker.Notice);
        Assert.Empty(result.State.Session.Tabs);
    }

    [Fact]
    public void GivenExactlyOneBoardAndNoName_WhenRunningBacklog_ThenItOpensDirectly()
    {
        var state = Start();
        state = state with { Session = state.Session with { Sources = LoadedLists([_boards[0]]) } };

        var result = Run(state, "backlog");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Backlog, tab.Definition.Source);
    }

    [Fact]
    public void GivenListsNotLoaded_WhenRunningBoardWithAName_ThenItWaitsAndOpensWhenTheBoardsArrive()
    {
        var waiting = Run(Start(), "board gcm");

        Assert.Empty(waiting.State.Session.Tabs);
        Assert.True(waiting.State.Picker!.AutoOpen);
        Assert.IsType<LoadSourcesEffect>(Assert.Single(waiting.Effects));

        var arrived = _reducer.Reduce(waiting.State, new BoardsLoaded(_boards));

        var tab = Assert.Single(arrived.State.Session.Tabs);
        Assert.Equal("4", tab.Definition.SourceValue);
        Assert.Null(arrived.State.Picker);
        Assert.Equal(InputMode.Normal, arrived.State.Mode);
    }

    [Fact]
    public void GivenAWaitingCommand_WhenTheUserTypesBeforeTheReply_ThenNothingOpensWithoutEnter()
    {
        var waiting = Run(Start(), "board");
        var typed = Press(waiting.State, "g");

        var arrived = _reducer.Reduce(typed.State, new BoardsLoaded(_boards));

        Assert.Empty(arrived.State.Session.Tabs);
        Assert.NotNull(arrived.State.Picker);
    }

    [Fact]
    public void GivenAFilterThatIsFavouriteAndOwn_WhenMerging_ThenItAppearsOnceFirstAndStarred()
    {
        var own = new SavedFilter("10002", "Minefield", null, "Me", false, null);
        var starred = new SavedFilter("10002", "Minefield", null, "Me", true, null);
        var other = new SavedFilter("10001", "Mine", null, "Me", false, null);

        var merged = SourceChoices.MergeFilters([starred], [other, own]);

        Assert.Equal(["10002", "10001"], merged.Select(filter => filter.Id));
        Assert.True(merged[0].IsFavourite);
    }

    [Fact]
    public void GivenAWaitingCommand_WhenTheLoadFails_ThenThePickerStaysOpenAndShowsNoAutoOpen()
    {
        var waiting = Run(Start(), "filter mine");

        var failed = _reducer.Reduce(waiting.State, Failure(SourceListKind.Filters, JiraErrorKind.Forbidden));

        Assert.Empty(failed.State.Session.Tabs);
        Assert.False(failed.State.Picker!.AutoOpen);
        Assert.NotNull(failed.State.Session.Sources.FiltersError);
    }

    [Fact]
    public void GivenAWaitingCommand_WhenEscIsPressedBeforeTheReply_ThenTheLateReplyOpensNothing()
    {
        var waiting = Run(Start(), "board gcm");
        var cancelled = Press(waiting.State, "Esc");

        var late = _reducer.Reduce(cancelled.State, new BoardsLoaded(_boards));

        Assert.Empty(late.State.Session.Tabs);
        Assert.Null(late.State.Picker);
        Assert.True(late.State.Session.Sources.IsLoaded(SourceListKind.Boards));
    }

    [Fact]
    public void GivenTheProfileChanges_WhenAReplyForTheOldSiteIsKeptInTheSession_ThenTheNewSessionStartsEmpty()
    {
        var state = Loaded(Start());
        var other = new Profile("other", "https://other.atlassian.net", "me@other.test");

        var switched = _reducer.Reduce(
            state,
            new ProfilesChanged(ProfileCatalog.Empty.With(other), "Switched."));

        Assert.False(switched.State.Session.Sources.IsLoaded(SourceListKind.Boards));
        Assert.False(switched.State.Session.Sources.IsLoaded(SourceListKind.Filters));
    }

    [Fact]
    public void GivenJqlCommand_WhenRun_ThenTheQueryIsPassedAsTypedWithColonsAndQuotes()
    {
        var query = "summary ~ \"a: b\" AND status = 'Done' ORDER BY key";

        var result = Run(Start(), $"jql {query}");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(new ViewDefinition(ViewSourceKind.Jql, query, ViewLayout.List, query), tab.Definition);
        Assert.Empty(result.Effects.OfType<LoadSourcesEffect>());
    }

    [Fact]
    public void GivenTypedText_WhenThePickerFilters_ThenTheFirstMatchIsHighlightedAndEnterOpensIt()
    {
        var state = OpenPicker(Loaded(Start()));

        state = Type(state, "gcm");
        var choices = SourceChoices.For(state.Picker!, state.Session.Sources);

        Assert.Equal(SourceChoiceKind.Board, choices[0].Kind);
        Assert.Equal(["GCM board", "GCM board", "Filter for GCM board", "text ~ \"gcm\""], choices.Select(c => c.Name));

        var result = Press(state, "Enter");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Board, tab.Definition.Source);
        Assert.Null(result.State.Picker);
    }

    [Fact]
    public void GivenNothingMatches_WhenPressingEnter_ThenTheQueryRowOpensAsJql()
    {
        var state = Type(OpenPicker(Loaded(Start())), "zzz");

        var result = Press(state, "Enter");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal(ViewSourceKind.Jql, tab.Definition.Source);
        Assert.Equal("text ~ \"zzz\"", tab.Definition.SourceValue);
    }

    [Fact]
    public void GivenAJqlClauseTyped_WhenPressingEnter_ThenItIsSentAsTyped()
    {
        var state = Type(OpenPicker(Loaded(Start())), "assignee = currentUser()");

        var result = Press(state, "Enter");

        Assert.Equal("assignee = currentUser()", Assert.Single(result.State.Session.Tabs).Definition.SourceValue);
    }

    [Fact]
    public void GivenEmptyTextAndNoLists_WhenPressingEnter_ThenNothingOpensAndTheNoticeSaysWhy()
    {
        var state = OpenPicker(Start());

        var result = Press(state, "Enter");

        Assert.Empty(result.State.Session.Tabs);
        Assert.NotNull(result.State.Picker);
        Assert.Equal("Still loading: wait a moment, or press Esc.", result.State.Picker.Notice);
    }

    [Fact]
    public void GivenThePicker_WhenMovingDownAndUp_ThenTheHighlightStopsAtTheEnds()
    {
        var state = OpenPicker(Loaded(Start()));
        var count = SourceChoices.For(state.Picker!, state.Session.Sources).Count;

        var up = Press(state, "Up");
        var down = state;

        for (var index = 0; index < count + 3; index++)
        {
            down = Press(down, "Ctrl-n").State;
        }

        Assert.Equal(0, up.State.Picker!.Selected);
        Assert.Equal(count - 1, down.Picker!.Selected);
        Assert.Equal(count - 2, Press(down, "Ctrl-p").State.Picker!.Selected);
    }

    [Fact]
    public void GivenAHighlightedSecondRow_WhenEnterIsPressed_ThenThatRowOpens()
    {
        var state = Press(OpenPicker(Loaded(Start())), "Down").State;

        var result = Press(state, "Enter");

        Assert.Equal(ViewSourceKind.Backlog, Assert.Single(result.State.Session.Tabs).Definition.Source);
    }

    [Fact]
    public void GivenTypedText_WhenBackspacing_ThenTheLastCharacterGoesAndTheHighlightResets()
    {
        var state = Type(OpenPicker(Loaded(Start())), "gc");
        state = Press(state, "Down").State;

        var result = Press(state, "Backspace");

        Assert.Equal("g", result.State.Picker!.Text);
        Assert.Equal(0, result.State.Picker.Selected);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenEmptyText_WhenBackspacing_ThenThePickerStaysOpen()
    {
        var result = Press(OpenPicker(Loaded(Start())), "Backspace");

        Assert.NotNull(result.State.Picker);
    }

    [Fact]
    public void GivenThePicker_WhenPressingEscOrCtrlC_ThenItClosesAndNeverQuits()
    {
        var opened = OpenPicker(Loaded(Start()));

        var escaped = Press(opened, "Esc");
        var interrupted = Press(opened, "Ctrl-c");

        Assert.Null(escaped.State.Picker);
        Assert.Equal(InputMode.Normal, escaped.State.Mode);
        Assert.Null(interrupted.State.Picker);
        Assert.Empty(interrupted.Effects.OfType<QuitEffect>());
    }

    [Fact]
    public void GivenThePicker_WhenPasting_ThenTheFirstLineFillsTheFilter()
    {
        var opened = OpenPicker(Loaded(Start()));

        var result = _reducer.Reduce(opened, new TextPasted("gtm\nsecond line"));

        Assert.Equal("gtm", result.State.Picker!.Text);
    }

    [Fact]
    public void GivenThePickerAndAnOtherTabActive_WhenAReplyForATabArrives_ThenThePickerIsUntouched()
    {
        var withTab = Run(Start(), "jql project = A").State;
        var picker = OpenPicker(Loaded(withTab));
        var tab = picker.Session.Tabs[0];

        var result = _reducer.Reduce(picker, new ViewLoadFailed(tab.Id, tab.RequestId, Error(JiraErrorKind.Network), DateTimeOffset.UnixEpoch));

        Assert.NotNull(result.State.Picker);
        Assert.Equal(InputMode.Insert, result.State.Mode);
    }

    [Fact]
    public void GivenUnknownCommand_WhenRun_ThenTheMessageIsKept()
    {
        var result = Run(Start(), "boards");

        Assert.Equal("Unknown command: boards", result.State.Message);
    }

    [Fact]
    public void GivenJqlWithoutQuery_WhenRunWithoutProfile_ThenTheMessageSaysToAddOne()
    {
        var result = Run(AppState.Initial, "jql");

        Assert.Null(result.State.Picker);
        Assert.Equal("No profile yet: add one with :profile add.", result.State.Message);
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private static SourceLists LoadedLists(IReadOnlyList<BoardSummary> boards)
    {
        return new SourceLists(boards, _filters, false, false, null, null);
    }

    private static AppState Loaded(AppState state)
    {
        return state with { Session = state.Session with { Sources = LoadedLists(_boards) } };
    }

    private static SourceListFailed Failure(SourceListKind kind, JiraErrorKind errorKind)
    {
        return new SourceListFailed(kind, Error(errorKind));
    }

    private static JiraError Error(JiraErrorKind kind)
    {
        return new JiraError(kind, null, "failed");
    }

    private AppState OpenPicker(AppState state)
    {
        return Press(Press(state, "Space").State, "t").State;
    }

    private AppState Type(AppState state, string text)
    {
        var current = state;

        foreach (var character in text)
        {
            current = Press(current, character == ' ' ? "Space" : character.ToString()).State;
        }

        return current;
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Run(AppState state, string command)
    {
        var result = Press(state, ":");

        foreach (var character in command)
        {
            result = Press(result.State, character == ' ' ? "Space" : character.ToString());
        }

        return Press(result.State, "Enter");
    }
}
