using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Writes;

[Trait("Os", "Independent")]
public class RankBlockTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
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
    public void GivenTwoMarkedRows_WhenPressingAltJ_ThenTheyAskFirstAndThenMoveAsOneBlockInOrder()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4"), 0, 1);

        var asked = Press(state, "Alt-j");
        var confirmed = Press(asked.State, "y");

        Assert.Equal(InputMode.Confirm, asked.State.Mode);
        Assert.Contains("A-1, A-2", asked.State.Confirm!.Message);
        Assert.Contains("Jira has no undo", asked.State.Confirm.Message);
        var request = RequestOf(confirmed);
        Assert.Equal(["A-1", "A-2"], request.Keys);
        Assert.Equal("A-3", request.ReferenceKey);
        Assert.False(request.Before);
        Assert.Equal(["A-3", "A-1", "A-2", "A-4"], Keys(confirmed.State));
    }

    [Fact]
    public void GivenTwoMarkedRows_WhenPressingAltK_ThenTheBlockGoesAboveTheRowBeforeIt()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4"), 2, 3);

        var request = RequestOf(Press(Press(state, "Alt-k").State, "y"));

        Assert.Equal(["A-3", "A-4"], request.Keys);
        Assert.Equal("A-2", request.ReferenceKey);
        Assert.True(request.Before);
    }

    [Fact]
    public void GivenScatteredMarks_WhenPressingAltJ_ThenTheBlockIsGatheredBelowTheNextUnmarkedRowKeepingOrder()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4", "1:A-5"), 0, 2);

        var result = Press(Press(state, "Alt-j").State, "y");

        Assert.Equal(["A-2", "A-4", "A-1", "A-3", "A-5"], Keys(result.State));
        Assert.Equal(["A-1", "A-3"], RequestOf(result).Keys);
        Assert.Equal("A-4", RequestOf(result).ReferenceKey);
    }

    [Fact]
    public void GivenACount_WhenPressingAltJWithASet_ThenTheBlockMovesThatManyPlaces()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4", "1:A-5"), 0, 1);

        var counted = Press(Press(state, "2").State, "Alt-j");
        var result = Press(counted.State, "y");

        Assert.Equal(["A-3", "A-4", "A-1", "A-2", "A-5"], Keys(result.State));
        Assert.Equal("A-4", RequestOf(result).ReferenceKey);
    }

    [Fact]
    public void GivenAVisualRange_WhenPressingAltJ_ThenTheRangeMovesAndStaysSelectedAsMarks()
    {
        var state = Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4");
        var ranged = Press(Press(state, "v").State, "j").State;

        var result = Press(Press(ranged, "Alt-j").State, "y");

        Assert.Equal(["A-3", "A-1", "A-2", "A-4"], Keys(result.State));
        Assert.Equal(["A-1", "A-2"], Tab(result.State).Marks);
        Assert.Null(Tab(result.State).VisualAnchor);
    }

    [Fact]
    public void GivenABlockAtTheBottom_WhenPressingAltJ_ThenItIsRefusedWithoutAWrite()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), 1, 2);

        var result = Press(state, "Alt-j");

        Assert.Equal("The selected issues are already at the bottom", result.State.Message);
        Assert.Empty(result.Effects);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenABlockAtTheTop_WhenPressingAltK_ThenItIsRefusedWithoutAWrite()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), 0, 1);

        var result = Press(state, "Alt-k");

        Assert.Equal("The selected issues are already at the top", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenOneMark_WhenPressingAltJ_ThenItMovesWithoutAsking()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), 0);

        var result = Press(state, "Alt-j");

        var request = RequestOf(result);
        Assert.Equal(["A-1"], request.Keys);
        Assert.Equal("A-2", request.ReferenceKey);
    }

    [Theory]
    [InlineData("Space k t")]
    [InlineData("Space k b")]
    [InlineData("Space k k")]
    public void GivenAMarkedSet_WhenPressingRankTopBottomOrTo_ThenItRefusesWithoutAWrite(string keys)
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), 0, 1);

        var result = Chord(state, keys);

        Assert.Equal("2 issues selected; clear the selection first", result.State.Message);
        Assert.Empty(result.Effects);
        Assert.Empty(result.State.Session.Operations);
    }

    [Fact]
    public void GivenMoreThanFiftyMarks_WhenPressingAltJ_ThenItRefuses()
    {
        var specs = Enumerable.Range(1, 52).Select(number => $"1:A-{number}").ToArray();
        var state = Marked(Loaded(ViewLayout.List, specs), [.. Enumerable.Range(0, 51)]);

        var result = Press(state, "Alt-j");

        Assert.Equal("At most 50 issues can be ranked at once: clear some marks", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenMarksInTwoColumns_WhenPressingAltJ_ThenItRefusesBecauseTheyAreNotInOneColumn()
    {
        var state = Marked(Loaded(ViewLayout.Columns, "1:A-1", "1:A-2", "2:A-3"), 0, 2);

        var result = Press(state, "Alt-j");

        Assert.Contains("rank one column at a time", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenMarksInOneBoardColumn_WhenPressingAltK_ThenTheBlockMovesAboveTheCardInTheColumn()
    {
        var state = Marked(Loaded(ViewLayout.Columns, "1:A-1", "2:A-2", "1:A-3", "1:A-4", "1:A-5"), 2, 3);

        var result = Press(Press(state, "Alt-k").State, "y");

        var request = RequestOf(result);
        Assert.Equal(["A-3", "A-4"], request.Keys);
        Assert.Equal("A-1", request.ReferenceKey);
        Assert.True(request.Before);
        Assert.Equal(["A-3", "A-4", "A-1", "A-2", "A-5"], Keys(result.State));
    }

    [Fact]
    public void GivenAMarkedParentWithAChildInItsColumn_WhenPressingAltJ_ThenTheBlockLandsBelowTheNextCard()
    {
        var state = Loaded(ViewLayout.Columns, "1:A-1", "1:A-2", "1:A-3");
        var tab = Tab(state);
        Assert.Equal("A-1", tab.Rows[0].Key);
        var withChild = state with
        {
            Session = state.Session with
            {
                Tabs =
                [
                    tab with
                    {
                        Rows = [tab.Rows[0], tab.Rows[1] with { Parent = new IssueParent("A-1", null) }, tab.Rows[2]],
                    },
                ],
            },
        };

        var result = Press(Marked(withChild, 0), "Alt-j");

        Assert.Equal("A-3", RequestOf(result).ReferenceKey);
        Assert.False(RequestOf(result).Before);
    }

    [Fact]
    public void GivenAScatteredSet_WhenPressingAltKWithAnOvershootingCount_ThenTheBlockGoesToTheTop()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4", "1:A-5"), 2, 4);

        var counted = Press(Press(state, "9").State, "Alt-k");
        var result = Press(counted.State, "y");

        Assert.Equal(["A-3", "A-5", "A-1", "A-2", "A-4"], Keys(result.State));
        Assert.Equal("A-1", RequestOf(result).ReferenceKey);
        Assert.True(RequestOf(result).Before);
    }

    [Fact]
    public void GivenABlockThatJiraRefusesAsInvalid_WhenTheWriteFails_ThenTheOrderGoesBackAndTheViewReloads()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), 0, 1);
        var started = Press(Press(state, "Alt-j").State, "y");
        var operation = Assert.Single(started.State.Session.Operations);
        var error = new WriteError(WriteErrorKind.Validation, null, "1 of 2 not ranked (A-2): no", [], []);

        var failed = _reducer.Reduce(started.State, new WriteFailed(operation.Id, _now, error));

        Assert.Equal(["A-1", "A-2", "A-3"], Keys(failed.State));
        Assert.Equal("Could not rank 2 issues: 1 of 2 not ranked (A-2): no", failed.State.Message);
        Assert.Empty(failed.State.Session.Operations);
        Assert.NotEmpty(failed.Effects);
    }

    [Fact]
    public void GivenARejectedBlock_WhenTheWriteFails_ThenTheRowsGoBackToTheirOldOrder()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3", "1:A-4"), 0, 2);
        var started = Press(Press(state, "Alt-j").State, "y");
        var operation = Assert.Single(started.State.Session.Operations);

        var failed = _reducer.Reduce(
            started.State,
            new WriteFailed(operation.Id, _now, new WriteError(WriteErrorKind.Permission, null, "no", [], [])));

        Assert.Equal(["A-1", "A-2", "A-3", "A-4"], Keys(failed.State));
        Assert.Empty(failed.State.Session.Operations);
        Assert.StartsWith("No permission to rank 2 issues", failed.State.Message);
    }

    [Fact]
    public void GivenABlockInFlight_WhenRankingItAgain_ThenItIsBusy()
    {
        var state = Marked(Loaded(ViewLayout.List, "1:A-1", "1:A-2", "1:A-3"), 0, 1);
        var started = Press(Press(state, "Alt-j").State, "y").State;

        var again = Press(started, "Alt-k");

        Assert.Contains("is busy (ranking)", again.State.Message);
    }

    private static RankBlockRequest RequestOf(ReduceResult result)
    {
        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));

        return Assert.IsType<RankBlockRequest>(effect.Request);
    }

    private static string[] Keys(AppState state)
    {
        return [.. Tab(state).Rows.Select(row => row.Key)];
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(candidate => candidate.Id == state.Session.Active);
    }

    private static AppState Marked(AppState state, params int[] indexes)
    {
        var tab = Tab(state);
        var marks = indexes.Select(index => tab.Rows[index].Key).ToArray();
        var changed = tab with { Marks = marks };
        var tabs = state.Session.Tabs.Select(candidate => candidate.Id == changed.Id ? changed : candidate).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

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

    private AppState Loaded(ViewLayout layout, params string[] specs)
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };
        var source = layout == ViewLayout.Columns ? ViewSourceKind.Board : ViewSourceKind.Backlog;
        var definition = new ViewDefinition(source, "4", layout, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var viewport = _reducer.Reduce(opened, new ViewportChanged(60)).State;
        var tab = Tab(viewport);
        var configured = _reducer.Reduce(
            viewport,
            new BoardConfigurationLoaded(tab.Id, tab.RequestId, _configuration)).State;
        var rows = specs.Select(Row).ToArray();

        return _reducer.Reduce(
            configured,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, rows.Length), _now)).State;
    }
}
