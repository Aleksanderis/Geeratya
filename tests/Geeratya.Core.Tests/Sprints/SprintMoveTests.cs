using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.Sources;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Tests.Backlog;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Sprints;

[Trait("Os", "Independent")]
public class SprintMoveTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenOneIssue_WhenMovingToTheBacklog_ThenItMovesAtOnceWithoutAskingAndTheRowsRegroup()
    {
        var state = At(Loaded(), "A-3");

        var result = Chord(state, "Space s b");

        var request = Request(result);
        Assert.Equal(["A-3"], request.Keys);
        Assert.Null(request.Target);
        Assert.Equal(InputMode.Normal, result.State.Mode);
        Assert.Null(Tab(result.State).Rows.Single(row => row.Key == "A-3").CurrentSprint);
        Assert.Equal("A-3", Tab(result.State).Rows[^3].Key);
    }

    [Fact]
    public void GivenMarkedIssues_WhenMovingToTheBacklog_ThenTheQuestionNamesCountSourceAndTargetAndNoUndo()
    {
        var state = Marked(Loaded(), "A-2", "A-3");

        var result = Chord(state, "Space s b");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Empty(result.Effects);
        var question = result.State.Confirm!.Message;
        Assert.Contains("Move 2 issues (A-3, A-2)", question, StringComparison.Ordinal);
        Assert.Contains(
            "sprint \"Sprint 1\" and sprint \"Sprint 2\" to the backlog",
            question,
            StringComparison.Ordinal);
        Assert.Contains("Jira has no undo", question, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenTheQuestion_WhenAnsweringNoOrEsc_ThenNothingIsSentAndTheRowsStay()
    {
        var asked = Chord(Marked(Loaded(), "A-2", "A-3"), "Space s b").State;

        var declined = Press(asked, "n");
        var escaped = Press(asked, "Esc");

        foreach (var result in new[] { declined, escaped })
        {
            Assert.Empty(result.Effects);
            Assert.Equal(InputMode.Normal, result.State.Mode);
            Assert.Empty(result.State.Session.Operations);
            Assert.Equal(["A-2", "A-3"], Tab(result.State).Marks);
            var row = Tab(result.State).Rows.Single(candidate => candidate.Key == "A-2");

            Assert.Equal(BacklogFixtures.FutureId, row.CurrentSprint!.Id);
        }
    }

    [Fact]
    public void GivenTheQuestion_WhenAnsweringYes_ThenOneWriteStartsForTheMarkedIssuesOnly()
    {
        var asked = Chord(Marked(At(Loaded(), "A-5"), "A-2", "A-3"), "Space s b").State;

        var result = Press(asked, "y");

        Assert.Equal(["A-3", "A-2"], Request(result).Keys);
        Assert.Single(result.State.Session.Operations);
    }

    [Fact]
    public void GivenAVisualRange_WhenMoving_ThenTheRangeIsTheSetNotTheCursorRowAlone()
    {
        var state = Press(At(Loaded(), "A-3"), "v").State;
        state = Press(Press(state, "j").State, "j").State;

        var result = Press(Chord(state, "Space s b").State, "y");

        Assert.Equal(["A-2", "A-3", "A-4"], Request(result).Keys.OrderBy(key => key));
    }

    [Fact]
    public void GivenASprintPicker_WhenChoosingASprint_ThenOnlyActiveAndFutureSprintsAreOfferedAndTheMoveStarts()
    {
        var opened = Chord(At(Loaded(), "A-5"), "Space s m");
        var sprints = new[] { Closed(1), BacklogFixtures.Future, BacklogFixtures.Active };
        var loaded = _reducer.Reduce(opened.State, new SprintListLoaded(6, sprints)).State;

        var names = SourceChoices.For(loaded.Picker!, loaded.Session.Sources).Select(row => row.Name).ToArray();
        var moved = Press(loaded, "Enter");

        Assert.Equal(InputMode.Insert, opened.State.Mode);
        Assert.Equal(["A-5"], opened.State.Picker!.Sprint!.MoveKeys);
        Assert.Single(opened.Effects.OfType<LoadSprintsEffect>());
        Assert.Equal(["Sprint 1", "Sprint 2"], names);
        var request = Request(moved);
        Assert.Equal(BacklogFixtures.ActiveId, request.Target!.Id);
        Assert.Equal(["A-5"], request.Keys);
        Assert.Equal(InputMode.Normal, moved.State.Mode);
        Assert.Equal("Sprint 1", Tab(moved.State).Rows.Single(row => row.Key == "A-5").CurrentSprint!.Name);
    }

    [Fact]
    public void GivenAPickerForSeveralIssues_WhenChoosingASprint_ThenItAsksBeforeMoving()
    {
        var opened = Chord(Marked(Loaded(), "A-1", "A-5"), "Space s m");
        var loaded = _reducer.Reduce(opened.State, new SprintListLoaded(6, [BacklogFixtures.Future])).State;

        var chosen = Press(loaded, "Enter");

        Assert.Equal(InputMode.Confirm, chosen.State.Mode);
        Assert.Contains("to sprint \"Sprint 2\"", chosen.State.Confirm!.Message, StringComparison.Ordinal);
        Assert.Empty(chosen.Effects);
    }

    [Fact]
    public void GivenMarksAndACursorElsewhere_WhenMoving_ThenTheCursorRowIsNotAdded()
    {
        var state = At(Marked(Loaded(), "A-1", "A-2"), "A-5");

        var result = Press(Chord(state, "Space s b").State, "y");

        Assert.Equal(["A-1", "A-2"], Request(result).Keys.OrderBy(key => key));
    }

    [Fact]
    public void GivenTheCursorOnACollapsedSectionHeader_WhenMoving_ThenItIsRefusedInsteadOfMovingTheHiddenRow()
    {
        var state = Press(At(Loaded(), "A-3"), "z").State;
        state = Press(state, "a").State;

        var result = Chord(state, "Space s b");

        Assert.Empty(result.Effects);
        Assert.Contains("Expand the section first", result.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAJqlTab_WhenMoving_ThenItIsRefusedWithAReason()
    {
        var state = Loaded(ViewSourceKind.Jql);

        var result = Chord(state, "Space s b");

        Assert.Empty(result.Effects);
        Assert.Equal("Sprint moves work in a board, backlog or sprint view", result.State.Message);
    }

    [Fact]
    public void GivenAKanbanBoard_WhenMoving_ThenItIsRefused()
    {
        var state = WithBoard(Loaded(ViewSourceKind.Backlog), BoardType.Kanban);

        var result = Chord(state, "Space s m");

        Assert.Null(result.State.Picker);
        Assert.Equal("This is a Kanban board: it has no sprints.", result.State.Message);
    }

    [Fact]
    public void GivenAClosedSprintView_WhenMoving_ThenItIsReadOnly()
    {
        var state = Loaded(ViewSourceKind.Sprint);
        var closed = Tab(state) with { Sprint = Closed(3) };
        state = Replace(state, closed);

        var result = Chord(state, "Space s b");

        Assert.Equal(ClosedSprintGate.Refusal, result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAMoveJiraRefused_WhenTheWriteFails_ThenTheRowsGoBackToTheirSprintsAndGroups()
    {
        var started = Chord(At(Loaded(), "A-3"), "Space s b");
        var id = started.State.Session.Operations.Single().Id;
        var error = new WriteError(WriteErrorKind.Validation, null, "Sprint is closed", [], ["Sprint is closed"]);

        var failed = _reducer.Reduce(started.State, new WriteFailed(id, _now, error));

        Assert.Empty(failed.State.Session.Operations);
        Assert.Contains("Sprint is closed", failed.State.Message, StringComparison.Ordinal);
        var restored = Tab(failed.State).Rows.Single(row => row.Key == "A-3");

        Assert.Equal(BacklogFixtures.ActiveId, restored.CurrentSprint!.Id);
        // The rank inside the section comes back with the reload that follows the refusal.
        Assert.Equal(
            BacklogFixtures.RankedRows.Select(row => row.Key).Order(),
            Tab(failed.State).Rows.Select(row => row.Key).Order());
        Assert.IsType<Loading>(Tab(failed.State).Load);
    }

    [Fact]
    public void GivenAMoveThatSucceeded_WhenSettled_ThenMarksClearAndTheTabReloads()
    {
        var started = Press(Chord(Marked(Loaded(), "A-2", "A-3"), "Space s b").State, "y");
        var id = started.State.Session.Operations.Single().Id;

        var done = _reducer.Reduce(started.State, new WriteSucceeded(id, _now, []));

        Assert.Empty(Tab(done.State).Marks);
        Assert.Contains(done.Effects, effect => effect is not InvalidateCacheEffect);
        Assert.Equal("Moved 2 issues to the backlog", done.State.Message);
    }

    [Fact]
    public void GivenARunningMove_WhenMovingTheSameIssueAgain_ThenItIsBusy()
    {
        var started = Chord(At(Loaded(), "A-3"), "Space s b");

        var again = Chord(started.State, "Space s b");

        Assert.Contains("A-3 is busy", again.State.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAMoveThatTookSomeIssues_WhenItFails_ThenOnlyTheIssuesNotMovedStayMarkedAndTheTabReloads()
    {
        var started = Press(Chord(Marked(Loaded(), "A-1", "A-2", "A-3"), "Space s b").State, "y");
        var id = started.State.Session.Operations.Single().Id;
        var error = new WriteError(WriteErrorKind.Validation, null, "1 of 3 moved", [], ["x"], null, ["A-2", "A-3"]);

        var failed = _reducer.Reduce(started.State, new WriteFailed(id, _now, error));

        Assert.Equal(["A-2", "A-3"], Tab(failed.State).Marks);
        Assert.Null(Tab(failed.State).VisualAnchor);
        Assert.IsType<Loading>(Tab(failed.State).Load);
    }

    [Fact]
    public void GivenAMoveThatTookSomeIssues_WhenTheCallEndedWithARateLimit_ThenTheTabStillReloads()
    {
        var started = Press(Chord(Marked(Loaded(), "A-1", "A-2"), "Space s b").State, "y");
        var id = started.State.Session.Operations.Single().Id;
        var error = new WriteError(WriteErrorKind.RateLimited, 429, "slow down", [], ["x"], null, ["A-2"]);

        var failed = _reducer.Reduce(started.State, new WriteFailed(id, _now, error));

        Assert.Equal(["A-2"], Tab(failed.State).Marks);
        Assert.IsType<Loading>(Tab(failed.State).Load);
    }

    [Fact]
    public void GivenAMoveThatTookNothing_WhenItFails_ThenAllMarksStay()
    {
        var started = Press(Chord(Marked(Loaded(), "A-2", "A-3"), "Space s b").State, "y");
        var id = started.State.Session.Operations.Single().Id;
        var error = new WriteError(WriteErrorKind.Validation, null, "closed", [], ["closed"], null, ["A-2", "A-3"]);

        var failed = _reducer.Reduce(started.State, new WriteFailed(id, _now, error));

        Assert.Equal(["A-2", "A-3"], Tab(failed.State).Marks);
    }

    [Fact]
    public void GivenTheOpenIssueAndMarksElsewhere_WhenMoving_ThenOnlyTheOpenIssueMovesAndTheMarksStay()
    {
        var detail = _reducer.Reduce(At(Marked(Loaded(), "A-1", "A-2"), "A-5"), new KeyPressed("Enter", _now));
        var opened = Chord(detail.State, "Space s b");

        Assert.NotEmpty(Tab(detail.State).Detail);
        var request = Request(opened);
        Assert.Equal(["A-5"], request.Keys);
        Assert.Equal(["A-1", "A-2"], Tab(opened.State).Marks);
    }

    [Fact]
    public void GivenTheSprintPickerInMoveMode_WhenClickingARow_ThenThatSprintIsChosenLikeEnter()
    {
        var opened = Chord(At(Loaded(), "A-5"), "Space s m");
        var loaded = _reducer.Reduce(opened.State, new SprintListLoaded(6, [BacklogFixtures.Future, BacklogFixtures.Active])).State;
        var rows = SourceChoices.For(loaded.Picker!, loaded.Session.Sources);
        var index = rows.ToList().FindIndex(row => row.Name == BacklogFixtures.Active.Name);

        var clicked = _reducer.Reduce(loaded, new OverlayRowClicked(MouseTarget.PickerRow, index, ActionIds.FormNext, _now));

        var request = Request(clicked);
        Assert.Equal(BacklogFixtures.ActiveId, request.Target!.Id);
        Assert.Equal(["A-5"], request.Keys);
        Assert.Equal(InputMode.Normal, clicked.State.Mode);
    }

    [Fact]
    public void GivenARow_WhenOpeningTheContextMenu_ThenBothMoveEntriesShowOnBoardsOnly()
    {
        var board = ContextMenus.For(Loaded())!;
        var jql = ContextMenus.For(Loaded(ViewSourceKind.Jql))!;

        Assert.Contains(board.Items, item => item.Action == ActionIds.SprintMoveIssues);
        Assert.Contains(board.Items, item => item.Action == ActionIds.SprintMoveToBacklog);
        Assert.DoesNotContain(jql.Items, item => item.Action == ActionIds.SprintMoveIssues);
    }

    private static Sprint Closed(long id)
    {
        return new Sprint(id, $"Closed {id}", SprintState.Closed, null, _now, _now, _now, 6);
    }

    private static SprintMoveRequest Request(ReduceResult result)
    {
        var effect = Assert.Single(result.Effects.OfType<ExecuteWriteEffect>());

        return Assert.IsType<SprintMoveRequest>(effect.Request);
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static AppState Replace(AppState state, TabState tab)
    {
        var tabs = state.Session.Tabs.Select(candidate => candidate.Id == tab.Id ? tab : candidate).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static AppState Marked(AppState state, params string[] keys)
    {
        return Replace(state, Tab(state) with { Marks = keys });
    }

    private static AppState WithBoard(AppState state, BoardType type)
    {
        var board = new BoardConfiguration(6, "Board", type, [], "1", null, null, null);

        return Replace(state, Tab(state) with { Board = board });
    }

    private static AppState At(AppState state, string key)
    {
        var tab = Tab(state);
        var index = tab.Rows.ToList().FindIndex(row => row.Key == key);

        return Replace(state, tab with { Selected = index, SelectedKey = key });
    }

    private ReduceResult Press(AppState state, string key)
    {
        return _reducer.Reduce(state, new KeyPressed(key, _now));
    }

    private ReduceResult Chord(AppState state, string keys)
    {
        var result = new ReduceResult(state, []);

        foreach (var key in keys.Split(' '))
        {
            result = Press(result.State, key);
        }

        return result;
    }

    private AppState Loaded(ViewSourceKind source = ViewSourceKind.ScrumBacklog)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };
        var value = source == ViewSourceKind.Jql ? "project = A" : "6";
        var definition = new ViewDefinition(source, value, ViewLayout.List, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var viewport = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = Tab(viewport);
        var rows = BacklogSections.Order(BacklogFixtures.RankedRows);

        return _reducer.Reduce(
            viewport,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, rows.Count), _now)).State;
    }
}
