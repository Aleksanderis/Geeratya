using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Writes;

[Trait("Os", "Independent")]
public class RankKeysTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Doing", ["2"])],
        "10001",
        null,
        "customfield_10019",
        null);

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenBacklogList_WhenPressingAltJ_ThenTheRowMovesDownAtOnceAndTheRankWriteIsSent()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3");

        var result = Press(state, "Alt-j");

        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));
        var request = Assert.IsType<RankRequest>(effect.Request);
        Assert.Equal(new RankRequest("A-1", "A-2", false, "customfield_10019", null, "A-2"), request);
        Assert.Equal(["A-2", "A-1", "A-3"], Keys(result.State));
        Assert.Equal("A-1", Tab(result.State).SelectedKey);
        Assert.Equal(1, Tab(result.State).Selected);
        Assert.Equal("Ranking A-1 below A-2", result.State.Message);
        Assert.IsType<OrderRowChange>(Assert.Single(result.State.Session.Operations).Undo[0]);
    }

    [Fact]
    public void GivenBacklogList_WhenPressingAltKAtTheTop_ThenItIsRefusedWithoutAWrite()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2");

        var result = Press(state, "Alt-k");

        Assert.Equal("A-1 is already at the top", result.State.Message);
        Assert.Empty(result.Effects);
        Assert.Empty(result.State.Session.Operations);
        Assert.Equal(["A-1", "A-2"], Keys(result.State));
    }

    [Fact]
    public void GivenTheLastRow_WhenPressingAltJ_ThenItIsRefused()
    {
        var state = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2"), "j").State;

        var result = Press(state, "Alt-j");

        Assert.Equal("A-2 is already at the bottom", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenACount_WhenPressingAltJ_ThenTheIssueMovesThatManyPlacesInOneWrite()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4");

        var typed = Press(state, "2");
        var result = Press(typed.State, "Alt-j");

        Assert.Equal(2, typed.State.Count);
        Assert.Equal(0, result.State.Count);
        var request = RequestOf(result);
        Assert.Equal("A-3", request.ReferenceKey);
        Assert.False(request.Before);
        Assert.Equal(["A-2", "A-3", "A-1", "A-4"], Keys(result.State));
    }

    [Fact]
    public void GivenACountLargerThanTheList_WhenPressingAltJ_ThenItStopsAtTheEnd()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3");

        var result = Press(Press(Press(state, "9").State, "9").State, "Alt-j");

        Assert.Equal(["A-2", "A-3", "A-1"], Keys(result.State));
    }

    [Fact]
    public void GivenACount_WhenPressingJ_ThenTheSelectionMovesThatManyRows()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4");

        var result = Press(Press(state, "3").State, "j");

        Assert.Equal("A-4", Tab(result.State).SelectedKey);
    }

    [Fact]
    public void GivenALoneZero_WhenPressed_ThenItStaysFirstColumnAndAfterADigitItIsPartOfTheCount()
    {
        var board = Loaded(ViewSourceKind.Board, ViewLayout.Columns, "1:A-1", "2:A-2");
        var right = Press(board, "l").State;

        var first = Press(right, "0").State;
        var counted = Press(Press(right, "1").State, "0").State;

        Assert.Equal(0, Tab(first).Cursor!.Column);
        Assert.Equal(10, counted.Count);
    }

    [Fact]
    public void GivenACount_WhenPressingEscOrAnyKey_ThenTheCountIsDropped()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2");

        var esc = Press(Press(state, "4").State, "Esc");
        var unbound = Press(Press(state, "4").State, "z");

        Assert.Equal(0, esc.State.Count);
        Assert.Equal(0, unbound.State.Count);
    }

    [Fact]
    public void GivenACount_WhenPressingRankToTop_ThenItIsRefusedBecauseCountsApplyToStepsOnly()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3");
        var selected = Press(Press(state, "j").State, "j").State;

        var result = Press(Press(Press(Press(selected, "3").State, "Space").State, "k").State, "t");

        Assert.Equal("A-3", Tab(result.State).SelectedKey);
        Assert.Empty(result.Effects);
        Assert.Equal(["A-1", "A-2", "A-3"], Keys(result.State));
        Assert.StartsWith("A count does not apply to", result.State.Message);
    }

    [Fact]
    public void GivenSpaceKT_WhenPressed_ThenTheIssueGoesAboveTheFirstRow()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3");
        var selected = Press(Press(state, "j").State, "j").State;

        var result = Press(Press(Press(selected, "Space").State, "k").State, "t");

        var request = RequestOf(result);
        Assert.Equal("A-1", request.ReferenceKey);
        Assert.True(request.Before);
        Assert.Equal("A-2", request.WasAfter);
        Assert.Equal(["A-3", "A-1", "A-2"], Keys(result.State));
    }

    [Fact]
    public void GivenSpaceKB_WhenPressed_ThenTheIssueGoesBelowTheLastRow()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3");

        var result = Press(Press(Press(state, "Space").State, "k").State, "b");

        var request = RequestOf(result);
        Assert.Equal("A-3", request.ReferenceKey);
        Assert.False(request.Before);
        Assert.Equal(["A-2", "A-3", "A-1"], Keys(result.State));
    }

    [Fact]
    public void GivenRowsStillLoading_WhenRankingToTheBottom_ThenItIsRefusedBecauseTheEndIsNotKnown()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2");
        var partial = With(state, tab => tab with { Next = new PageCursor("t") });

        var result = Press(Press(Press(partial, "Space").State, "k").State, "b");

        Assert.Empty(result.Effects);
        Assert.StartsWith("Not every issue is loaded", result.State.Message);
    }

    [Fact]
    public void GivenABoardWithoutARankField_WhenRanking_ThenTheRequestLetsJiraPickTheField()
    {
        var state = Loaded(ViewSourceKind.Board, ViewLayout.List, "1:A-1", "1:A-2");
        var team = With(state, tab => tab with { Board = _configuration with { RankFieldId = null } });

        var result = Press(team, "Alt-j");

        Assert.Null(RequestOf(result).RankFieldId);
    }

    [Fact]
    public void GivenAJqlList_WhenRanking_ThenItIsRefusedBecauseItsOrderIsNotTheRank()
    {
        var state = Loaded(ViewSourceKind.Jql, ViewLayout.List, "1:A-1", "1:A-2");

        var result = Press(state, "Alt-j");

        Assert.Equal("Ranking works in a board or backlog view", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenColumnsLayout_WhenPressingAltJ_ThenTheCardGoesBelowTheNextCardOfItsColumnAndSaysWhatLiesBetween()
    {
        var state = Loaded(ViewSourceKind.Board, ViewLayout.Columns, "1:A-1", "2:B-1", "2:B-2", "1:A-2", "1:A-3");

        var result = Press(state, "Alt-j");

        var request = RequestOf(result);
        Assert.Equal("A-2", request.ReferenceKey);
        Assert.Equal(2, request.HiddenBetween);
        Assert.Equal(["B-1", "B-2", "A-2", "A-1", "A-3"], Keys(result.State));
        Assert.Equal("A-1", Tab(result.State).SelectedKey);
        Assert.Equal(1, ColumnsNavigation.SelectedColumn(Tab(result.State))!.Index);
        Assert.Equal("Ranking A-1 below A-2 (2 cards of other columns lie between)", result.State.Message);
    }

    [Fact]
    public void GivenAParentWithAChildInItsColumn_WhenPressingAltJ_ThenTheWholeGroupMovesPastTheNextCard()
    {
        var state = Loaded(ViewSourceKind.Board, ViewLayout.Columns, "1:A-1", "1:A-2", "1:A-3");
        var withChild = With(state, tab => tab with
        {
            Rows = [tab.Rows[0], tab.Rows[1] with { Parent = new IssueParent("A-1", null) }, tab.Rows[2]],
        });

        var down = Press(withChild, "Alt-j");
        var up = Press(Press(Press(withChild, "j").State, "j").State, "Alt-k");

        var request = RequestOf(down);
        Assert.Equal("A-3", request.ReferenceKey);
        Assert.False(request.Before);
        var upRequest = RequestOf(up);
        Assert.Equal("A-3", upRequest.Key);
        Assert.Equal("A-1", upRequest.ReferenceKey);
        Assert.True(upRequest.Before);
    }

    [Fact]
    public void GivenAParentWithItsChildAtTheBottom_WhenPressingAltJ_ThenItIsAlreadyAtTheBottom()
    {
        var state = Loaded(ViewSourceKind.Board, ViewLayout.Columns, "1:A-1", "1:A-2");
        var withChild = With(state, tab => tab with
        {
            Rows = [tab.Rows[0], tab.Rows[1] with { Parent = new IssueParent("A-1", null) }],
        });

        var result = Press(withChild, "Alt-j");

        Assert.Equal("A-1 is already at the bottom", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenColumnsLayout_WhenRankingInAnEmptyColumn_ThenNothingHappens()
    {
        var state = Loaded(ViewSourceKind.Board, ViewLayout.Columns, "1:A-1", "1:A-2");
        var wider = _configuration with { Columns = [.. _configuration.Columns, new BoardColumn("Done", ["4"])] };
        var empty = With(state, tab => tab with { Board = wider });
        var moved = Press(Press(Press(empty, "l").State, "l").State, "Alt-j");

        Assert.Empty(moved.Effects);
        Assert.Equal("Nothing to rank here", moved.State.Message);
    }

    [Fact]
    public void GivenARankInFlight_WhenRankingTheSameIssueAgain_ThenItIsRefusedAsBusy()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");

        var again = Press(started.State, "Alt-j");

        Assert.Equal("A-1 is busy (ranking)", again.State.Message);
        Assert.Empty(again.Effects);
        Assert.Equal(["A-2", "A-1", "A-3"], Keys(again.State));
    }

    [Fact]
    public void GivenAFailedRank_WhenTheOperationEnds_ThenTheRowIsPutBackAndTheReasonIsShown()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");

        var failed = Reply(started.State, new WriteError(WriteErrorKind.Permission, 403, "No", [], ["No"]));

        Assert.Equal(["A-1", "A-2", "A-3"], Keys(failed.State));
        Assert.Equal("A-1", Tab(failed.State).SelectedKey);
        Assert.Equal(0, Tab(failed.State).Selected);
        Assert.Equal("No permission to rank A-1: No", failed.State.Message);
        Assert.Empty(failed.State.Session.Operations);
        Assert.False(Tab(failed.State).RefreshQueued);
    }

    [Fact]
    public void GivenARankRefusedBecauseTheReferenceIsGone_WhenTheOperationEnds_ThenTheRowIsPutBackAndTheViewReloads()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");

        var error = new WriteError(WriteErrorKind.Validation, 400, "Gone", [], ["Issue does not exist"]);

        var failed = Reply(started.State, error);

        Assert.Equal(["A-1", "A-2", "A-3"], Keys(failed.State));
        Assert.Contains(failed.Effects, effect => effect is LoadViewPageEffect);
        Assert.IsType<Loading>(Tab(failed.State).Load);
    }

    [Fact]
    public void GivenAFailedRankAfterANewerOrderArrived_WhenRollingBack_ThenTheNewerOrderIsKept()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");
        var reloaded = With(started.State, tab => tab with { Rows = Rows("1:A-3", "1:A-1", "1:A-2") });

        var failed = Reply(reloaded, new WriteError(WriteErrorKind.Permission, 403, "No", [], []));

        Assert.Equal(["A-3", "A-1", "A-2"], Keys(failed.State));
    }

    [Fact]
    public void GivenARankThatSucceeded_WhenTheOperationEnds_ThenTheViewReloadsAndTheStatusLineSaysRanked()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");

        var done = _reducer.Reduce(started.State, new WriteSucceeded(Operation(started.State).Id, _now, []));

        Assert.Equal("Ranked A-1 below A-2", done.State.Message);
        Assert.Equal(["A-2", "A-1", "A-3"], Keys(done.State));
        Assert.Empty(done.State.Session.Operations);
        Assert.Contains(done.Effects, effect => effect is LoadViewPageEffect);
        Assert.IsType<Loading>(Tab(done.State).Load);
    }

    [Fact]
    public void GivenALostReply_WhenTheCheckCannotTellAnOrder_ThenTheOrderGoesBackAndTheViewReloads()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");
        var id = Operation(started.State).Id;
        var unknown = _reducer.Reduce(started.State, new WriteOutcomeUnknown(id, _now, Error(WriteErrorKind.Timeout)));

        var checking = unknown.State.Message;
        var done = _reducer.Reduce(unknown.State, new WriteVerified(id, _now, VerifyOutcome.Unreachable, []));

        Assert.Equal("A-1: not sure the rank worked, checking", checking);
        Assert.Equal(["A-1", "A-2", "A-3"], Keys(done.State));
        Assert.Equal("A-1: could not check the result; reload to see the current state", done.State.Message);
        Assert.Contains(done.Effects, effect => effect is LoadViewPageEffect);
    }

    [Fact]
    public void GivenAListLoadLandingMidRank_WhenItsReplyHasTheOldOrder_ThenNoRowIsLostAndTheReloadFixesTheOrder()
    {
        var started = Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "Alt-j");
        var refreshing = With(started.State, tab => tab with
        {
            Load = new Loading(LoadReason.Refresh),
            RequestId = 900,
        });

        var landed = _reducer.Reduce(
            refreshing,
            new ViewPageLoaded(Tab(refreshing).Id, 900, new IssuePage(Rows("1:A-1", "1:A-2", "1:A-3"), null, 3), _now));
        var done = _reducer.Reduce(landed.State, new WriteSucceeded(Operation(landed.State).Id, _now, []));

        Assert.Equal(["A-1", "A-2", "A-3"], Keys(landed.State));
        Assert.Equal("A-1", Tab(done.State).SelectedKey);
        Assert.Empty(done.State.Session.Operations);
        Assert.Contains(done.Effects, effect => effect is LoadViewPageEffect);
        Assert.IsType<Loading>(Tab(done.State).Load);
    }

    [Fact]
    public void GivenTwoTabsOnTheBoard_WhenRanking_ThenBothShowTheNewOrderAndAJqlTabKeepsItsOrder()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3");
        var withJql = _reducer.Reduce(state, new ViewOpenRequested(new ViewDefinition(ViewSourceKind.Jql, "x", ViewLayout.List, "x"))).State;
        var jql = Tab(withJql);
        var loadedJql = _reducer.Reduce(withJql, new ViewPageLoaded(jql.Id, jql.RequestId, new IssuePage(Rows("1:A-1", "1:A-2", "1:A-3"), null, 3), _now)).State;
        var backlog = loadedJql.Session.Tabs[0];
        var active = loadedJql with { Session = loadedJql.Session with { Active = backlog.Id } };

        var result = Press(active, "Alt-j");

        Assert.Equal(["A-2", "A-1", "A-3"], result.State.Session.Tabs[0].Rows.Select(row => row.Key));
        Assert.Equal(["A-1", "A-2", "A-3"], result.State.Session.Tabs[1].Rows.Select(row => row.Key));
    }

    [Fact]
    public void GivenRankTo_WhenPickingUpAndDroppingBelowTheCursor_ThenTheIssueTakesThePlaceOfTheTarget()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4");

        var picked = RankTo(state);
        var moved = Press(Press(picked.State, "j").State, "j");
        var dropped = RankTo(moved.State);

        Assert.StartsWith("Picked up A-1", picked.State.Message);
        Assert.Contains("<space>kk", picked.State.Message);
        Assert.Empty(picked.Effects);
        var request = RequestOf(dropped);
        Assert.Equal(new RankRequest("A-1", "A-3", false, "customfield_10019", null, "A-2"), request);
        Assert.Equal(["A-2", "A-3", "A-1", "A-4"], Keys(dropped.State));
        Assert.Null(Tab(dropped.State).RankPickup);
    }

    [Fact]
    public void GivenRankTo_WhenDroppingAboveThePickedUpIssue_ThenItGoesBeforeTheTarget()
    {
        var state = Press(Press(Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), "j").State, "j").State;

        var picked = RankTo(state);
        var up = Press(Press(picked.State, "k").State, "k");
        var dropped = RankTo(up.State);

        var request = RequestOf(dropped);
        Assert.Equal("A-1", request.ReferenceKey);
        Assert.True(request.Before);
        Assert.Equal(["A-3", "A-1", "A-2"], Keys(dropped.State));
    }

    [Fact]
    public void GivenAPickedUpIssue_WhenDroppingOnItselfOrPressingEsc_ThenNothingIsWritten()
    {
        var state = Loaded(ViewSourceKind.Backlog, ViewLayout.List, "1:A-1", "1:A-2");
        var picked = RankTo(state);

        var onItself = RankTo(picked.State);
        var escaped = Press(picked.State, "Esc");

        Assert.Equal("Cancelled: nothing to move", onItself.State.Message);
        Assert.Null(Tab(onItself.State).RankPickup);
        Assert.Empty(onItself.Effects);
        Assert.Null(Tab(escaped.State).RankPickup);
        Assert.Empty(escaped.State.Session.Operations);
    }

    [Fact]
    public void GivenAPickedUpCard_WhenDroppingInAnotherColumn_ThenItIsRefusedAndThePickupIsForgotten()
    {
        var state = Loaded(ViewSourceKind.Board, ViewLayout.Columns, "1:A-1", "2:B-1");
        var picked = RankTo(state);

        var dropped = RankTo(Press(picked.State, "l").State);

        Assert.Empty(dropped.Effects);
        Assert.Contains("A-1 is not in this column", dropped.State.Message);
        Assert.Null(Tab(dropped.State).RankPickup);
    }

    private static RankRequest RequestOf(ReduceResult result)
    {
        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));

        return Assert.IsType<RankRequest>(effect.Request);
    }

    private static string[] Keys(AppState state)
    {
        return [.. Tab(state).Rows.Select(row => row.Key)];
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static Operation Operation(AppState state)
    {
        return Assert.Single(state.Session.Operations);
    }

    private static AppState With(AppState state, Func<TabState, TabState> change)
    {
        var changed = change(Tab(state));
        var tabs = state.Session.Tabs.Select(tab => tab.Id == changed.Id ? changed : tab).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static WriteError Error(WriteErrorKind kind)
    {
        return new WriteError(kind, null, "x", [], []);
    }

    private static IssueSummary[] Rows(params string[] specs)
    {
        return [.. specs.Select(Row)];
    }

    /// <summary>A spec is <c>statusId:key</c>.</summary>
    private static IssueSummary Row(string spec)
    {
        var parts = spec.Split(':');

        return new IssueSummary(
            parts[1],
            parts[1],
            $"Summary {parts[1]}",
            new IssueStatus(parts[0], "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    private ReduceResult Reply(AppState state, WriteError error)
    {
        return _reducer.Reduce(state, new WriteFailed(Operation(state).Id, _now, error));
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _now));
    }

    private ReduceResult RankTo(AppState state)
    {
        var space = Press(state, "Space").State;
        var k = Press(space, "k").State;

        return Press(k, "k");
    }

    private AppState Loaded(ViewSourceKind source, ViewLayout layout, params string[] specs)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };
        var definition = new ViewDefinition(source, "4", layout, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var viewport = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = Tab(viewport);
        var configured = viewport;

        if (source is ViewSourceKind.Board or ViewSourceKind.Backlog)
        {
            configured = _reducer.Reduce(viewport, new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration)).State;
        }

        var rows = Rows(specs);

        return _reducer.Reduce(configured, new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, rows.Length), _now)).State;
    }
}
