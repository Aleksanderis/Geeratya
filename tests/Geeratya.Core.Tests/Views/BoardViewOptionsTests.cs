using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.Cache;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class BoardViewOptionsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Done", ["4"])],
        "10001",
        null,
        null,
        null);

    private static readonly QuickFilter[] _filters =
    [
        new(5, "Recently Updated", "updatedDate >= -1d", 0),
        new(6, "Assigned to me", "assignee = currentUser() OR reporter = currentUser()", 1),
    ];

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenFiltersNotRead_WhenPressingTheFilterKey_ThenTheyAreRequestedForThisBoardAndNothingReloads()
    {
        var result = PressKeys(Loaded(Rows()), "Space", "v", "f");

        var effect = Assert.IsType<LoadQuickFiltersEffect>(Assert.Single(result.Effects));
        Assert.True(effect.Apply);
        Assert.Equal(Tab(result.State).Id, effect.Tab);
        Assert.Equal(BoardViewOptions.FiltersLoadingText, result.State.Message);
    }

    [Fact]
    public void GivenFiltersArrivedFromTheKey_WhenApplying_ThenTheFirstFilterIsOnAndTheBoardReloadsWithItsJql()
    {
        var loaded = Loaded(Rows());

        var result = _reducer.Reduce(loaded, new QuickFiltersLoaded(Tab(loaded).Id, _filters, true));

        var tab = Tab(result.State);
        var load = LoadOf(result);
        Assert.Equal([5L], tab.ActiveQuickFilters);
        Assert.Equal("(updatedDate >= -1d)", load.ExtraJql);
        Assert.Equal(LoadReason.Open, load.Reason);
        Assert.Empty(tab.Rows);
        Assert.Equal(tab.RequestId, load.RequestId);
        Assert.Contains(result.Effects, effect => effect is CancelTabEffect);
        Assert.Equal("Quick filter: Recently Updated.", result.State.Message);
    }

    [Fact]
    public void GivenOneFilterOn_WhenPressingTheKeyTwice_ThenTheSecondThenNoneAreOn()
    {
        var first = PressKeys(WithFilters(Loaded(Rows())), "Space", "v", "f");
        var second = PressKeys(first.State, "Space", "v", "f");
        var off = PressKeys(second.State, "Space", "v", "f");

        Assert.Equal([6L], Tab(second.State).ActiveQuickFilters);
        Assert.Equal("(assignee = currentUser() OR reporter = currentUser())", LoadOf(second).ExtraJql);
        Assert.Empty(Tab(off.State).ActiveQuickFilters);
        Assert.Null(LoadOf(off).ExtraJql);
        Assert.Equal("Quick filters off.", off.State.Message);
    }

    [Fact]
    public void GivenTwoFiltersByCommand_WhenLoading_ThenTheClausesAreAndedEachInParentheses()
    {
        var state = WithFilters(Loaded(Rows()));

        var one = Run(state, "quickfilter 1");
        var both = Run(one.State, "quickfilter assigned to me");

        Assert.Equal([5L, 6L], Tab(both.State).ActiveQuickFilters);
        Assert.Equal(
            "(updatedDate >= -1d) AND (assignee = currentUser() OR reporter = currentUser())",
            LoadOf(both).ExtraJql);
    }

    [Fact]
    public void GivenFiltersOn_WhenRunningOffOrAnUnknownName_ThenOffClearsAndTheUnknownIsRefusedWithoutLoading()
    {
        var on = Run(WithFilters(Loaded(Rows())), "quickfilter 2").State;

        var unknown = Run(on, "quickfilter nope");
        var listed = Run(on, "quickfilter");
        var off = Run(on, "quickfilter off");

        Assert.Empty(unknown.Effects);
        Assert.Equal($"No quick filter \"nope\". {BoardViewOptions.FilterUsage}", unknown.State.Message);
        Assert.Empty(listed.Effects);
        Assert.Contains("2 Assigned to me (on)", listed.State.Message);
        Assert.Empty(Tab(off.State).ActiveQuickFilters);
    }

    [Fact]
    public void GivenABoardWithNoFilters_WhenPressingTheKey_ThenItSaysSoAndLoadsNothing()
    {
        var state = Loaded(Rows());
        var known = _reducer.Reduce(state, new QuickFiltersLoaded(Tab(state).Id, [], false)).State;

        var result = PressKeys(known, "Space", "v", "f");

        Assert.Empty(result.Effects);
        Assert.Equal(BoardViewOptions.NoFiltersText, result.State.Message);
    }

    [Fact]
    public void GivenALookupThatFailed_WhenItArrives_ThenTheStatusLineSaysWhyAndNothingChanges()
    {
        var state = Loaded(Rows());

        var result = _reducer.Reduce(state, new QuickFiltersLoaded(Tab(state).Id, [], true, "Cannot reach Jira."));

        Assert.Equal("Cannot reach Jira.", result.State.Message);
        Assert.False(Tab(result.State).QuickFiltersKnown);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAFilterOn_WhenRefreshingAndPagingAndWritingTheCache_ThenEveryLoadCarriesItAndNothingIsCached()
    {
        var on = PressKeys(WithFilters(Loaded(Rows())), "Space", "v", "f").State;
        var tab = Tab(on);

        var page = _reducer.Reduce(on, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(Rows(), null, 2), _now));
        var refresh = PressKeys(page.State, "Space", "r");

        Assert.DoesNotContain(page.Effects, effect => effect is WriteCacheEffect);
        Assert.Equal("(updatedDate >= -1d)", LoadOf(refresh).ExtraJql);
    }

    [Fact]
    public void GivenNoFilterOn_WhenThePageArrives_ThenItIsStillWrittenToTheCache()
    {
        var state = Loaded(Rows(), out var effects);

        Assert.Contains(effects, effect => effect is WriteCacheEffect);
        Assert.Empty(Tab(state).ActiveQuickFilters);
    }

    [Fact]
    public void GivenAJqlTab_WhenUsingTheFilterKeys_ThenTheyAreRefused()
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "Query");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition)).State;

        var filter = PressKeys(opened, "Space", "v", "f");
        var lane = PressKeys(opened, "Space", "v", "s");

        Assert.Equal(BoardViewOptions.NoBoardText, filter.State.Message);
        Assert.Equal(BoardViewOptions.NoBoardText, lane.State.Message);
        Assert.Empty(filter.Effects);
    }

    [Fact]
    public void GivenSwimlanesByAssignee_WhenStepping_ThenOneLaneShowsAtATimeAndTheSelectionStaysInIt()
    {
        var state = Loaded(Rows());

        var lanes = PressKeys(state, "Space", "v", "s").State;
        var first = ColumnsNavigation.ColumnsOf(Tab(lanes));
        var next = PressKeys(lanes, "]", "]").State;
        var second = ColumnsNavigation.ColumnsOf(Tab(next));
        var back = PressKeys(next, "[", "[").State;

        Assert.Equal(SwimlaneKind.Assignee, Tab(lanes).Swimlane);
        Assert.Equal(["A-1", "A-3"], first.SelectMany(column => column.Rows).Select(row => row.Key));
        Assert.Equal("A-1", Tab(lanes).SelectedKey);
        Assert.Equal(["A-2", "A-4"], second.SelectMany(column => column.Rows).Select(row => row.Key));
        Assert.Equal("A-2", Tab(next).SelectedKey);
        Assert.Equal("A-1", Tab(back).SelectedKey);
        Assert.Equal("Lane 2/2: Bo", BoardViewOptions.Summary(Tab(next), "|"));
    }

    [Fact]
    public void GivenMarkedCards_WhenTheLaneChanges_ThenTheMarksAreDroppedSoNoWriteHitsAHiddenCard()
    {
        var lanes = PressKeys(Loaded(Rows()), "Space", "v", "s").State;
        var marked = lanes with { Session = lanes.Session with { Tabs = [Tab(lanes) with { Marks = ["A-1", "A-3"] }] } };

        var next = PressKeys(marked, "]", "]").State;

        Assert.Empty(Tab(next).Marks);
    }

    [Fact]
    public void GivenSwimlanes_WhenCyclingPastEpicAndBack_ThenAllCardsShowAgain()
    {
        var state = Loaded(Rows());

        var assignee = PressKeys(state, "Space", "v", "s").State;
        var epic = PressKeys(assignee, "Space", "v", "s");
        var none = PressKeys(epic.State, "Space", "v", "s");

        Assert.Equal(SwimlaneKind.Epic, Tab(epic.State).Swimlane);
        Assert.Equal("Swimlanes by epic: 1 lane (]] and [[ change the lane).", epic.State.Message);
        Assert.Equal(SwimlaneKind.None, Tab(none.State).Swimlane);
        Assert.Equal(4, ColumnsNavigation.ColumnsOf(Tab(none.State)).Sum(column => column.Rows.Count));
        Assert.Equal(string.Empty, BoardViewOptions.Summary(Tab(none.State), "|"));
    }

    [Fact]
    public void GivenNoGrouping_WhenPressingTheLaneKeys_ThenTheyKeepTheirOtherMeaning()
    {
        var state = Loaded(Rows());

        var result = PressKeys(state, "]", "]");

        Assert.Null(BoardViewOptions.StepLane(state, 1));
        Assert.Equal(SwimlaneKind.None, Tab(result.State).Swimlane);
    }

    [Fact]
    public void GivenListLayout_WhenGroupingIntoLanes_ThenItIsRefused()
    {
        var loaded = Loaded(Rows());
        var list = _reducer.Reduce(loaded, new ViewLayoutChanged(Tab(loaded).Id, ViewLayout.List)).State;

        var result = PressKeys(list, "Space", "v", "s");

        Assert.Equal(BoardViewOptions.NoColumnsText, result.State.Message);
        Assert.Equal(SwimlaneKind.None, Tab(result.State).Swimlane);
    }

    [Fact]
    public void GivenFilterAndLane_WhenSummarising_ThenBothAreNamed()
    {
        var on = PressKeys(WithFilters(Loaded(Rows())), "Space", "v", "f").State;
        var tab = Tab(on) with { Swimlane = SwimlaneKind.Assignee, Rows = Rows() };

        Assert.Equal("Filter: Recently Updated|Lane 1/2: Al", BoardViewOptions.Summary(tab, "|"));
    }

    private static LoadViewPageEffect LoadOf(ReduceResult result)
    {
        return Assert.IsType<LoadViewPageEffect>(result.Effects.Single(effect => effect is LoadViewPageEffect));
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static AppState Start()
    {
        return AppState.Initial with
        {
            Profiles = ProfileCatalog.Empty.With(_work),
            Session = SiteSession.Empty(_work),
        };
    }

    /// <summary>Two columns; A-1 and A-3 are Al's, A-2 and A-4 Bo's.</summary>
    private static IssueSummary[] Rows()
    {
        return
        [
            Row("A-1", "1", "u1", "Al"),
            Row("A-2", "1", "u2", "Bo"),
            Row("A-3", "4", "u1", "Al"),
            Row("A-4", "4", "u2", "Bo"),
        ];
    }

    private static IssueSummary Row(string key, string status, string account, string name)
    {
        return new IssueSummary(
            key,
            key,
            $"Summary {key}",
            new IssueStatus(status, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            new IssueAssignee(account, name),
            [],
            null,
            null,
            "A");
    }

    private AppState Loaded(IssueSummary[] rows)
    {
        return Loaded(rows, out _);
    }

    private AppState Loaded(IssueSummary[] rows, out IReadOnlyList<Effect> pageEffects)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = _reducer.Reduce(Start(), new ViewOpenRequested(definition)).State;
        var sized = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = Tab(sized);
        var configured = _reducer.Reduce(sized, new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration));
        var page = new IssuePage(rows, null, rows.Length);
        var result = _reducer.Reduce(configured.State, new ViewPageLoaded(tab.Id, tab.RequestId, page, _now));
        pageEffects = result.Effects;

        return result.State;
    }

    private AppState WithFilters(AppState state)
    {
        return _reducer.Reduce(state, new QuickFiltersLoaded(Tab(state).Id, _filters, false)).State;
    }

    private ReduceResult PressKeys(AppState state, params string[] keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys)
        {
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return result;
    }

    private ReduceResult Run(AppState state, string command)
    {
        var result = _reducer.Reduce(state, new KeyPressed(":"));

        foreach (var character in command)
        {
            var key = character == ' ' ? "Space" : character.ToString(CultureInfo.InvariantCulture);
            result = _reducer.Reduce(result.State, new KeyPressed(key));
        }

        return _reducer.Reduce(result.State, new KeyPressed("Enter"));
    }
}
