using Geeratya.Core.History;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Jira.Boards.Models;

namespace Geeratya.Core.Tests.Sources;

[Trait("Os", "Independent")]
public class JqlHistoryReducerTests
{
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _home = new("home", "https://home.atlassian.net", "me@home.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenJqlCommand_WhenRun_ThenTheQueryIsRecordedForTheActiveProfile()
    {
        var result = Run(Start(), "jql project = A");

        var effect = Assert.Single(result.Effects.OfType<RecordHistoryEffect>());
        Assert.Equal(_work, effect.Profile);
        Assert.Equal(HistoryKind.Query, effect.Kind);
        Assert.Equal("project = A", effect.Text);
    }

    [Fact]
    public void GivenTheTypedQueryRowChosen_WhenAccepted_ThenTheGeneratedJqlIsRecorded()
    {
        var typed = Type(OpenPicker(Start()), "project = A");

        var result = Press(typed, "Enter");

        var effect = Assert.Single(result.Effects.OfType<RecordHistoryEffect>());
        Assert.Equal("project = A", effect.Text);
    }

    [Fact]
    public void GivenABoardChosen_WhenAccepted_ThenNothingIsRecorded()
    {
        var state = Loaded(OpenPicker(Start()), [new BoardSummary(4, "GCM board", BoardType.Kanban, "GCM")]);

        var result = Press(state, "Enter");

        Assert.DoesNotContain(result.Effects, effect => effect is RecordHistoryEffect);
    }

    [Fact]
    public void GivenLoadedHistory_WhenQueryOpened_ThenTheLoadedCopyHasItFirst()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("old = 1"));

        var result = Run(state, "jql new = 2");

        Assert.Equal(["new = 2", "old = 1"], result.State.Session.Sources.History!.Queries);
    }

    [Fact]
    public void GivenHistoryNotLoaded_WhenThePickerOpens_ThenItIsRequestedForTheProfile()
    {
        var result = Press(Press(Start(), "Space").State, "t");

        var effect = Assert.Single(result.Effects.OfType<LoadHistoryEffect>());
        Assert.Equal(_work, effect.Profile);
    }

    [Fact]
    public void GivenHistoryLoaded_WhenThePickerOpens_ThenItIsNotRequestedAgain()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("a = 1"));

        var result = Press(Press(state, "Space").State, "t");

        Assert.DoesNotContain(result.Effects, effect => effect is LoadHistoryEffect);
    }

    [Fact]
    public void GivenJqlCommandWithoutQuery_WhenRun_ThenTheJqlPickerListsRecentQueriesAndNoBoards()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("a = 1").WithQuery("b = 2"));

        var result = Run(state, "jql");

        var choices = SourceChoices.For(result.State.Picker!, result.State.Session.Sources);
        Assert.Equal(PickerScope.Jql, result.State.Picker!.Scope);
        Assert.Equal(["b = 2", "a = 1"], choices.Select(choice => choice.Name));
        Assert.All(choices, choice => Assert.Equal(SourceChoiceKind.Recent, choice.Kind));
        Assert.DoesNotContain(result.Effects, effect => effect is LoadSourcesEffect);
    }

    [Fact]
    public void GivenARecentRow_WhenAccepted_ThenItsQueryOpensAsAJqlTabAndMovesToTheFront()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("a = 1").WithQuery("b = 2"));
        var picker = Run(state, "jql").State;

        var chosen = Press(Press(picker, "Down").State, "Enter");

        var tab = Assert.Single(chosen.State.Session.Tabs);
        Assert.Equal("a = 1", tab.Definition.SourceValue);
        Assert.Equal(["a = 1", "b = 2"], chosen.State.Session.Sources.History!.Queries);
    }

    [Fact]
    public void GivenTypedText_WhenFilteringThePicker_ThenOnlyMatchingRecentQueriesAreOffered()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("project = GCM").WithQuery("project = GTM"));
        var typed = Type(Run(state, "jql").State, "gtm");

        var choices = SourceChoices.For(typed.Picker!, typed.Session.Sources);

        Assert.Equal(2, choices.Count);
        Assert.Equal(SourceChoiceKind.Recent, choices[0].Kind);
        Assert.Equal("project = GTM", choices[0].Name);
        Assert.Equal(SourceChoiceKind.Query, choices[^1].Kind);
    }

    [Fact]
    public void GivenManyRecentQueries_WhenTheFullPickerIsEmpty_ThenOnlyTheLatestTenAreOffered()
    {
        var history = SiteHistory.Empty;

        for (var index = 0; index < 30; index++)
        {
            history = history.WithQuery($"id = {index}");
        }

        var state = WithHistory(OpenPicker(Start()), history);

        var recent = SourceChoices.For(state.Picker!, state.Session.Sources)
            .Where(choice => choice.Kind == SourceChoiceKind.Recent)
            .ToList();

        Assert.Equal(10, recent.Count);
        Assert.Equal("id = 29", recent[0].Name);
    }

    [Fact]
    public void GivenAnEmptyJqlPicker_WhenAccepted_ThenNothingOpensAndANoticeShows()
    {
        var picker = Run(WithHistory(Start(), SiteHistory.Empty), "jql").State;

        var result = Press(picker, "Enter");

        Assert.Empty(result.State.Session.Tabs);
        Assert.NotNull(result.State.Picker!.Notice);
    }

    [Fact]
    public void GivenHistoryOfAnotherProfile_WhenItArrives_ThenItIsIgnored()
    {
        var reduced = _reducer.Reduce(Start(), new HistoryLoaded(_home, SiteHistory.Empty.WithQuery("home = 1"), 1));

        Assert.Null(reduced.State.Session.Sources.History);
    }

    [Fact]
    public void GivenHistoryOfTheActiveProfile_WhenItArrives_ThenItIsKept()
    {
        var reduced = _reducer.Reduce(Start(), new HistoryLoaded(_work, SiteHistory.Empty.WithQuery("work = 1"), 1));

        Assert.Equal(["work = 1"], reduced.State.Session.Sources.History!.Queries);
    }

    [Fact]
    public void GivenAnOlderReply_WhenItArrivesAfterANewerOne_ThenItIsDropped()
    {
        var newer = _reducer.Reduce(Start(), new HistoryLoaded(_work, SiteHistory.Empty.WithIssue("A-2").WithIssue("A-1"), 2));

        var stale = _reducer.Reduce(newer.State, new HistoryLoaded(_work, SiteHistory.Empty.WithIssue("A-1"), 1));

        Assert.Equal(["A-1", "A-2"], stale.State.Session.Sources.History!.Issues);
    }

    [Fact]
    public void GivenAProfileSwitch_WhenTheSessionIsReplaced_ThenTheOldHistoryIsGone()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("work = 1"));

        var switched = state with { Session = SiteSession.Empty(_home) };

        Assert.Null(switched.Session.Sources.History);
    }

    [Fact]
    public void GivenHistoryClearCommand_WhenRun_ThenTheLoadedCopyIsEmptyAndTheFileIsDeleted()
    {
        var state = WithHistory(Start(), SiteHistory.Empty.WithQuery("a = 1").WithIssue("A-1"));

        var result = Run(state, "history clear");

        var effect = Assert.Single(result.Effects.OfType<ClearHistoryEffect>());
        Assert.Equal(_work, effect.Profile);
        Assert.Empty(result.State.Session.Sources.History!.Queries);
        Assert.Empty(result.State.Session.Sources.History.Issues);
        Assert.Equal("History cleared.", result.State.Message);
    }

    [Fact]
    public void GivenNoProfile_WhenClearingHistory_ThenTheMessageSaysToAddOne()
    {
        var result = Run(AppState.Initial, "history clear");

        Assert.Empty(result.Effects);
        Assert.Equal("No profile yet: add one with :profile add.", result.State.Message);
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private static AppState WithHistory(AppState state, SiteHistory history)
    {
        return state with { Session = state.Session with { Sources = state.Session.Sources with { History = history } } };
    }

    private static AppState Loaded(AppState state, IReadOnlyList<BoardSummary> boards)
    {
        var lists = state.Session.Sources with { Boards = boards, Filters = [] };

        return state with { Session = state.Session with { Sources = lists } };
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
