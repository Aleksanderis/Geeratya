using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Tests.Backlog;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Tests.Writes;

/// <summary>Ranking in the Scrum backlog next to a row of another sprint section (#182).</summary>
[Trait("Os", "Independent")]
public class RankAcrossSectionsTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenTheLastRowOfASprint_WhenPressingAltJ_ThenItMovesIntoTheNextSectionAndSaysSo()
    {
        var result = Press(At(Loaded(), "A-4"), "Alt-j");

        var request = Request(result);
        Assert.Equal(["A-4"], request.Keys);
        Assert.Equal("A-2", request.ReferenceKey);
        Assert.False(request.Before);
        Assert.Equal(BacklogFixtures.FutureId, request.Target!.Id);
        Assert.Equal("Moving A-4 to sprint \"Sprint 2\", ranked below A-2", result.State.Message);
        var rows = Tab(result.State).Rows;
        Assert.Equal(["A-3", "A-2", "A-4", "A-6", "A-1", "A-5"], rows.Select(row => row.Key));
        Assert.Equal(BacklogFixtures.FutureId, rows[2].CurrentSprint!.Id);
    }

    [Fact]
    public void GivenTheFirstBacklogRow_WhenPressingAltK_ThenItMovesIntoTheSectionAbove()
    {
        var result = Press(At(Loaded(), "A-1"), "Alt-k");

        var request = Request(result);
        Assert.Equal("A-6", request.ReferenceKey);
        Assert.Equal(BacklogFixtures.FutureId, request.Target!.Id);
        Assert.True(request.Before);
    }

    [Fact]
    public void GivenARowInTheBacklog_WhenDroppingItOnARowOfASprint_ThenItTakesThatSprintAndPosition()
    {
        var picked = Chord(At(Loaded(), "A-5"), "Space k k").State;

        var result = Chord(At(picked, "A-3"), "Space k k");

        var request = Request(result);
        Assert.Equal(["A-5"], request.Keys);
        Assert.Equal("A-3", request.ReferenceKey);
        Assert.True(request.Before);
        Assert.Equal(BacklogFixtures.ActiveId, request.Target!.Id);
    }

    [Fact]
    public void GivenARowOfASprint_WhenDroppingItOnABacklogRow_ThenTheTargetIsTheBacklog()
    {
        var picked = Chord(At(Loaded(), "A-3"), "Space k k").State;

        var result = Chord(At(picked, "A-5"), "Space k k");

        var request = Request(result);
        Assert.Null(request.Target);
        Assert.Equal("the backlog", request.TargetText);
        Assert.False(request.Before);
        Assert.Null(Tab(result.State).Rows.Single(row => row.Key == "A-3").CurrentSprint);
    }

    [Fact]
    public void GivenAMoveInsideOneSection_WhenPressingAltJ_ThenItIsAPlainRank()
    {
        var result = Press(At(Loaded(), "A-3"), "Alt-j");

        Assert.IsType<RankRequest>(Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects)).Request);
    }

    [Fact]
    public void GivenMarkedIssuesOfTwoSections_WhenRankingDownAcrossABoundary_ThenOneQuestionNamesTheMove()
    {
        var loaded = Loaded();
        var marked = Replace(loaded, Tab(loaded) with { Marks = ["A-4", "A-6"] });

        var result = Press(At(marked, "A-4"), "Alt-j");

        Assert.Equal(InputMode.Confirm, result.State.Mode);
        Assert.Empty(result.Effects);
        var question = result.State.Confirm!.Message;
        Assert.Contains("Move 2 issues (A-4, A-6)", question, StringComparison.Ordinal);
        Assert.Contains("Two calls, Jira has no undo", question, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAFailedWrite_WhenItSettles_ThenTheRowIsBackInItsSectionAndTheTabReloads()
    {
        var started = Press(At(Loaded(), "A-4"), "Alt-j");
        var operation = Assert.Single(started.State.Session.Operations);
        var error = new WriteError(WriteErrorKind.Validation, null, "Moved to sprint but not ranked: Gone", [], []);

        var failed = _reducer.Reduce(started.State, new WriteFailed(operation.Id, _now, error));

        var rows = Tab(failed.State).Rows;
        Assert.Equal(["A-3", "A-4", "A-2", "A-6", "A-1", "A-5"], rows.Select(row => row.Key));
        Assert.Equal(BacklogFixtures.ActiveId, rows[1].CurrentSprint!.Id);
        Assert.Contains(failed.Effects, effect => effect is not ExecuteWriteEffect);
    }

    [Fact]
    public void GivenTheCursorOnACollapsedHeader_WhenRanking_ThenItIsRefusedWithoutAWrite()
    {
        var collapsed = Chord(Loaded(), "z a").State;

        var result = Press(collapsed, "Alt-j");

        Assert.Empty(result.Effects);
        Assert.Contains("Expand the section first", result.State.Message, StringComparison.Ordinal);
    }

    private static RankMoveRequest Request(ReduceResult result)
    {
        var effect = Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects));

        return Assert.IsType<RankMoveRequest>(effect.Request);
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

    private AppState Loaded()
    {
        var profiles = ProfileCatalog.Empty.With(_work);
        var start = AppState.Initial with
        {
            Profiles = profiles,
            Session = SiteSession.Empty(profiles.Active) with { Connection = new Connected("Me Myself", "acc-me") },
        };
        var definition = new ViewDefinition(ViewSourceKind.ScrumBacklog, "6", ViewLayout.List, "Board");
        var opened = _reducer.Reduce(start, new ViewOpenRequested(definition)).State;
        var viewport = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = Tab(viewport);
        var rows = BacklogSections.Order(BacklogFixtures.RankedRows);

        return _reducer.Reduce(
            viewport,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, rows.Count), _now)).State;
    }
}
