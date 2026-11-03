using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Tests.Sprints;

[Trait("Os", "Independent")]
public class SprintSwitcherTests
{
    private const long BoardId = 7;

    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly DateTimeOffset _day = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenABoardTab_WhenPressingSpaceSV_ThenThePickerOpensAndTheBoardsSprintsAreRequested()
    {
        var result = Press(Press(Press(OnBoard(), "Space"), "s"), "v");

        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(PickerScope.Sprints, result.State.Picker!.Scope);
        Assert.Equal(new LoadSprintsEffect(BoardId), Assert.Single(result.Effects.OfType<LoadSprintsEffect>()));
        Assert.Empty(SourceChoices.For(result.State.Picker, result.State.Session.Sources));
    }

    [Fact]
    public void GivenAJqlTab_WhenOpeningTheSwitcher_ThenNothingOpensAndTheNoteSaysWhy()
    {
        var jql = Open(Start(), new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A"));

        var result = SourceReducer.OpenSprintPicker(jql);

        Assert.Null(result.State.Picker);
        Assert.Empty(result.Effects);
        Assert.Equal("No board here: open a Scrum board first (:board).", result.State.Message);
    }

    [Fact]
    public void GivenLoadedSprints_WhenListingRows_ThenActiveFutureThenClosedNewestFirstAndOnlyAPage()
    {
        var picker = Loaded(OnBoard(), Sprints(closed: 12));

        var rows = SourceChoices.For(picker.Picker!, picker.Session.Sources);

        Assert.Equal(
            ["Active A", "Future F1", "Future F2"],
            rows.Take(3).Select(row => row.Name));
        Assert.Equal(["Closed 12", "Closed 11", "Closed 10"], rows.Skip(3).Take(3).Select(row => row.Name));
        Assert.Equal(SprintChoices.ClosedPage + 3 + 1, rows.Count);
        Assert.Equal(SourceChoiceKind.MoreClosed, rows[^1].Kind);
        Assert.Equal("2 more closed sprints", rows[^1].Name);
    }

    [Fact]
    public void GivenTheMoreRow_WhenAccepted_ThenAnotherPageOfClosedSprintsShowsAndThePickerStaysOpen()
    {
        var picker = Loaded(OnBoard(), Sprints(closed: 25));
        var onMore = Select(picker, SprintChoices.ClosedPage + 3);

        var result = Press(onMore, "Enter");

        var rows = SourceChoices.For(result.State.Picker!, result.State.Session.Sources);
        Assert.Equal(InputMode.Insert, result.State.Mode);
        Assert.Equal(3 + 20 + 1, rows.Count);
        Assert.Equal("Closed 6", rows[^2].Name);
        Assert.Equal("5 more closed sprints", rows[^1].Name);
        Assert.DoesNotContain(result.State.Session.Tabs, tab => tab.Definition.Source == ViewSourceKind.Sprint);
    }

    [Fact]
    public void GivenTheLastPage_WhenListingRows_ThenNoMoreRowShows()
    {
        var picker = Loaded(OnBoard(), Sprints(closed: 10));

        var rows = SourceChoices.For(picker.Picker!, picker.Session.Sources);

        Assert.DoesNotContain(rows, row => row.Kind == SourceChoiceKind.MoreClosed);
        Assert.Equal(3 + 10, rows.Count);
    }

    [Fact]
    public void GivenAFilter_WhenListingRows_ThenAllStatesAreSearchedByNameAndClosedStillPage()
    {
        var picker = Loaded(OnBoard(), Sprints(closed: 25));

        var future = Type(picker, "F1");
        var closed = Type(picker, "Closed 1");

        Assert.Equal(["Future F1"], Names(future));
        Assert.Equal(10, Names(closed).Count(name => name.StartsWith("Closed ", StringComparison.Ordinal)));
        Assert.Equal("1 more closed sprint", Names(closed)[^1]);
    }

    [Fact]
    public void GivenABoardTab_WhenAcceptingASprint_ThenItOpensInANewSprintTab()
    {
        var picker = Loaded(OnBoard(), Sprints(closed: 3));

        var result = Press(picker, "Enter");

        Assert.Equal(2, result.State.Session.Tabs.Count);
        var tab = result.State.Session.Tabs[1];
        Assert.Equal(ViewSourceKind.Sprint, tab.Definition.Source);
        Assert.Equal("7:100", tab.Definition.SourceValue);
        Assert.Equal("Active A", tab.Definition.Title);
        Assert.Equal(ViewLayout.Columns, tab.Definition.Layout);
        Assert.Null(result.State.Picker);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenASprintTab_WhenAcceptingAnotherSprint_ThenTheSameTabSwitches()
    {
        var onSprint = Open(Start(), SprintView(100, "Active A"));
        var picker = Loaded(onSprint, Sprints(closed: 3));
        var onClosed = Select(picker, 3);

        var result = Press(onClosed, "Enter");

        var tab = Assert.Single(result.State.Session.Tabs);
        Assert.Equal("7:3", tab.Definition.SourceValue);
        Assert.Equal("Closed 3", tab.Definition.Title);
        Assert.Contains(result.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenASprintTab_WhenPressingCtrlT_ThenTheSprintOpensInANewTab()
    {
        var onSprint = Open(Start(), SprintView(100, "Active A"));
        var picker = Loaded(onSprint, Sprints(closed: 3));
        var onFuture = Select(picker, 1);

        var result = Press(onFuture, "Ctrl-t");

        Assert.Equal(2, result.State.Session.Tabs.Count);
        Assert.Equal("7:100", result.State.Session.Tabs[0].Definition.SourceValue);
        Assert.Equal("7:101", result.State.Session.Tabs[1].Definition.SourceValue);
    }

    [Fact]
    public void GivenTheListIsStillLoading_WhenAccepting_ThenTheNoticeAsksToWait()
    {
        var opened = Press(Press(Press(OnBoard(), "Space"), "s"), "v").State;

        var result = Press(opened, "Enter");

        Assert.NotNull(result.State.Picker);
        Assert.Equal("Still loading: wait a moment, or press Esc.", result.State.Picker!.Notice);
    }

    [Fact]
    public void GivenAFailedLoad_WhenAccepting_ThenTheErrorIsTheNotice()
    {
        var opened = Press(Press(Press(OnBoard(), "Space"), "s"), "v").State;
        var error = new JiraError(JiraErrorKind.Forbidden, 403, "no");

        var failed = _reducer.Reduce(opened, new SprintsFailed(BoardId, error)).State;
        var result = Press(failed, "Enter");

        Assert.NotNull(result.State.Picker!.Sprint!.Error);
        Assert.Equal(result.State.Picker.Sprint.Error, result.State.Picker.Notice);
    }

    [Fact]
    public void GivenAReplyForAnotherBoard_WhenItArrives_ThenThePickerIsUntouched()
    {
        var opened = Press(Press(Press(OnBoard(), "Space"), "s"), "v").State;

        var result = _reducer.Reduce(opened, new SprintsLoaded(99, Sprints(closed: 1)));

        Assert.Null(result.State.Picker!.Sprint!.Sprints);
    }

    [Fact]
    public void GivenAReplyAfterTheClose_WhenItArrives_ThenNothingOpens()
    {
        var opened = Press(Press(Press(OnBoard(), "Space"), "s"), "v").State;
        var closed = Press(opened, "Esc").State;

        var result = _reducer.Reduce(closed, new SprintsLoaded(BoardId, Sprints(closed: 1)));

        Assert.Null(result.State.Picker);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenAClosedSprintTab_WhenAWriteKeyIsPressed_ThenItIsRefusedAsReadOnly()
    {
        var closed = WithSprint(Open(Start(), SprintView(1, "Closed 1")), SprintState.Closed);

        var status = Press(Press(Press(closed, "Space"), "i"), "s");
        var rank = Press(closed, "Alt-j");

        Assert.Equal(ClosedSprintGate.Refusal, status.State.Message);
        Assert.Equal(ClosedSprintGate.Refusal, rank.State.Message);
        Assert.Equal(InputMode.Normal, status.State.Mode);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("status next")]
    [InlineData("assign")]
    [InlineData("assign-me")]
    [InlineData("edit")]
    [InlineData("estimate")]
    [InlineData("create")]
    public void GivenAClosedSprintTab_WhenAWriteCommandIsTyped_ThenItIsRefusedAsReadOnly(string command)
    {
        var closed = WithSprint(Open(Start(), SprintView(1, "Closed 1")), SprintState.Closed);

        var result = Type(Press(closed, ":").State, command);
        var run = Press(result, "Enter");

        Assert.Equal(ClosedSprintGate.Refusal, run.State.Message);
        Assert.Equal(InputMode.Normal, run.State.Mode);
        Assert.Null(run.State.Form);
    }

    [Fact]
    public void GivenAClosedSprintTab_WhenALinkKeyIsPressed_ThenItIsRefused()
    {
        var closed = WithSprint(Open(Start(), SprintView(1, "Closed 1")), SprintState.Closed);

        var unlink = Press(Press(Press(closed, "Space"), "i"), "u");

        Assert.Equal(ClosedSprintGate.Refusal, unlink.State.Message);
    }

    [Fact]
    public void GivenLoadedSprints_WhenALateFailureArrives_ThenTheListKeepsItsErrorFreeState()
    {
        var picker = Loaded(OnBoard(), Sprints(closed: 1));

        var result = _reducer.Reduce(picker, new SprintsFailed(BoardId, new JiraError(JiraErrorKind.Network, null, "x")));

        Assert.Null(result.State.Picker!.Sprint!.Error);
        Assert.NotNull(result.State.Picker.Sprint.Sprints);
    }

    [Fact]
    public void GivenAnActiveSprintTab_WhenAWriteKeyIsPressed_ThenItIsNotRefused()
    {
        var active = WithSprint(Open(Start(), SprintView(100, "Active A")), SprintState.Active);

        var rank = Press(active, "Alt-j");

        Assert.NotEqual(ClosedSprintGate.Refusal, rank.State.Message);
    }

    [Fact]
    public void GivenTheViewMenu_WhenOnABoardOrAJqlTab_ThenSprintSwitchShowsOnlyOnTheBoard()
    {
        var board = ContextMenus.ForView(OnBoard())!;
        var jql = ContextMenus.ForView(Open(Start(), new ViewDefinition(ViewSourceKind.Jql, "x", ViewLayout.List, "x")))!;

        Assert.Contains(board.Items, item => item.Action == ActionIds.SprintSwitch);
        Assert.DoesNotContain(jql.Items, item => item.Action == ActionIds.SprintSwitch);
    }

    private static AppState Start()
    {
        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(_work), Session = SiteSession.Empty(_work) };
    }

    private static ViewDefinition SprintView(long sprint, string title)
    {
        return new ViewDefinition(ViewSourceKind.Sprint, $"{BoardId}:{sprint}", ViewLayout.Columns, title);
    }

    private static List<Sprint> Sprints(int closed)
    {
        var sprints = new List<Sprint>
        {
            Make(101, "Future F1", SprintState.Future, null),
            Make(100, "Active A", SprintState.Active, null),
            Make(102, "Future F2", SprintState.Future, null),
        };

        for (var number = 1; number <= closed; number++)
        {
            sprints.Add(Make(number, $"Closed {number}", SprintState.Closed, _day.AddDays(number)));
        }

        return sprints;
    }

    private static Sprint Make(long id, string name, SprintState state, DateTimeOffset? completed)
    {
        return new Sprint(id, name, state, null, _day, _day.AddDays(14), completed, BoardId);
    }

    private AppState Loaded(AppState board, IReadOnlyList<Sprint> sprints)
    {
        var opened = Press(Press(Press(board, "Space"), "s"), "v");

        return _reducer.Reduce(opened.State, new SprintsLoaded(BoardId, sprints)).State;
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

    private static List<string> Names(AppState state)
    {
        return [.. SourceChoices.For(state.Picker!, state.Session.Sources).Select(row => row.Name)];
    }

    private AppState Open(AppState state, ViewDefinition definition)
    {
        return _reducer.Reduce(state, new ViewOpenRequested(definition)).State;
    }

    private AppState OnBoard()
    {
        return Open(Start(), new ViewDefinition(ViewSourceKind.Board, "7", ViewLayout.Columns, "Growth board"));
    }

    private AppState WithSprint(AppState state, SprintState sprintState)
    {
        var tab = state.Session.Tabs[0];
        var sprint = Make(1, "S", sprintState, sprintState == SprintState.Closed ? _day : null);

        return _reducer.Reduce(state, new SprintLoaded(tab.Id, tab.RequestId, sprint)).State;
    }

    private AppState Select(AppState state, int index)
    {
        var current = state;

        for (var step = 0; step < index; step++)
        {
            current = Press(current, "Down").State;
        }

        return current;
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key));
    }

    private ReduceResult Press(ReduceResult result, string key)
    {
        return Press(result.State, key);
    }
}
