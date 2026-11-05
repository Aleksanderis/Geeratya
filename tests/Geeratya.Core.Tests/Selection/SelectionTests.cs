using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Selection;

[Trait("Os", "Independent")]
public class SelectionTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");

    private readonly AppReducer _reducer = new(DefaultKeymap.Load());

    [Fact]
    public void GivenAList_WhenPressingV_ThenVisualModeStartsOnTheCursorRowAndEscLeavesIt()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");

        var started = Press(state, "v").State;
        var left = Press(started, "Esc").State;

        Assert.Equal("A-1", Tab(started).VisualAnchor);
        Assert.Equal(1, RowSelection.Count(Tab(started)));
        Assert.Null(Tab(left).VisualAnchor);
    }

    [Fact]
    public void GivenVisualMode_WhenMovingDown_ThenTheRangeExtendsFromTheAnchor()
    {
        var state = Press(Loaded(ViewLayout.List, "A-1", "A-2", "A-3", "A-4"), "j").State;

        var started = Press(state, "v").State;
        var extended = Press(Press(started, "j").State, "j").State;
        var shrunk = Press(extended, "k").State;

        Assert.Equal(["A-2", "A-3", "A-4"], RowSelection.Targets(Tab(extended)));
        Assert.Equal(["A-2", "A-3"], RowSelection.Targets(Tab(shrunk)));
    }

    [Fact]
    public void GivenVisualModeMovingAboveTheAnchor_WhenReadingTheRange_ThenItRunsUpwards()
    {
        var state = Press(Press(Loaded(ViewLayout.List, "A-1", "A-2", "A-3"), "j").State, "j").State;

        var extended = Press(Press(Press(state, "v").State, "k").State, "k").State;

        Assert.Equal(["A-1", "A-2", "A-3"], RowSelection.Targets(Tab(extended)));
    }

    [Fact]
    public void GivenARowAndAMark_WhenPressingAltM_ThenTheRowIsMarkedAndTheCursorMovesDown()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");

        var marked = Press(state, "Alt-m").State;

        Assert.Equal(["A-1"], Tab(marked).Marks);
        Assert.Equal("A-2", Tab(marked).SelectedKey);
    }

    [Fact]
    public void GivenAMarkedRow_WhenMarkingItAgain_ThenItIsUnmarked()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(state, "Alt-m").State;
        var back = Press(marked, "k").State;

        var unmarked = Press(back, "Alt-m").State;

        Assert.Empty(Tab(unmarked).Marks);
    }

    [Fact]
    public void GivenScatteredMarks_WhenReadingTheTargets_ThenTheyComeInRowOrderAndWinOverTheCursorRow()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3", "A-4");
        var first = Press(state, "Alt-m").State;
        var third = Press(Press(first, "j").State, "Alt-m").State;

        Assert.Equal(["A-1", "A-3"], RowSelection.Targets(Tab(third)));
        Assert.Equal(2, RowSelection.Count(Tab(third)));
    }

    [Fact]
    public void GivenVisualModeAndAMark_WhenPressingAltM_ThenTheRangeIsMarkedAndVisualModeEnds()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var ranged = Press(Press(Press(state, "v").State, "j").State, "j").State;

        var marked = Press(ranged, "Alt-m").State;

        Assert.Equal(["A-1", "A-2", "A-3"], Tab(marked).Marks);
        Assert.Null(Tab(marked).VisualAnchor);
    }

    [Fact]
    public void GivenVisualModeAndMarks_WhenPressingEsc_ThenTheFirstLeavesTheRangeAndTheNextClearsTheMarks()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(state, "Alt-m").State;
        var visual = Press(marked, "v").State;

        var left = Press(visual, "Esc").State;
        var cleared = Press(left, "Esc").State;

        Assert.Null(Tab(left).VisualAnchor);
        Assert.Equal(["A-1"], Tab(left).Marks);
        Assert.Empty(Tab(cleared).Marks);
    }

    [Fact]
    public void GivenMarksAndAnIssueOpen_WhenPressingEsc_ThenTheStackPopsFirstAndTheMarksStay()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var marked = Press(state, "Alt-m").State;
        var opened = Press(marked, "Enter").State;

        var back = Press(opened, "Esc").State;

        Assert.Empty(Tab(back).Detail);
        Assert.Equal(["A-1"], Tab(back).Marks);
    }

    [Fact]
    public void GivenMarks_WhenARefreshBringsTheRowsBackInAnotherOrder_ThenTheMarksFollowTheKeys()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(state, "Alt-m").State;

        var reloaded = Reload(marked, "A-3", "A-1", "A-2");

        Assert.Equal(["A-1"], Tab(reloaded).Marks);
        Assert.Equal(["A-1"], RowSelection.Targets(Tab(reloaded)));
    }

    [Fact]
    public void GivenAMarkedIssueThatLeftTheResult_WhenTheWholeResultReloads_ThenItsMarkIsDropped()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(Press(state, "Alt-m").State, "Alt-m").State;

        var reloaded = Reload(marked, "A-2", "A-3");

        Assert.Equal(["A-2"], Tab(reloaded).Marks);
    }

    [Fact]
    public void GivenVisualModeAndAReload_WhenTheAnchorMoved_ThenTheRangeStillStartsAtTheAnchorIssue()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var ranged = Press(Press(state, "v").State, "j").State;

        var reloaded = Reload(ranged, "A-3", "A-1", "A-2");

        Assert.Equal(["A-1", "A-2"], RowSelection.Targets(Tab(reloaded)));
    }

    [Fact]
    public void GivenADeletedMarkedIssue_WhenTheRowsAreRemoved_ThenItsMarkGoes()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(Press(state, "Alt-m").State, "Alt-m").State;

        var removed = WriteRemoval.Apply(marked, ["A-1"]);

        Assert.Equal(["A-2"], Tab(removed).Marks);
    }

    [Fact]
    public void GivenAMarkedSet_WhenPressingASingleIssueWriteKey_ThenItRefusesAndNamesTheCount()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(Press(state, "Alt-m").State, "Alt-m").State;

        foreach (var keys in new[] { "Space i d", "Space i a", "Space i s", "Space k t", "Space i e" })
        {
            var result = Chord(marked, keys);

            Assert.Equal("2 issues selected; clear the selection first", result.State.Message);
            Assert.Empty(result.Effects);
            Assert.Equal(InputMode.Normal, result.State.Mode);
        }
    }

    [Fact]
    public void GivenOneMark_WhenPressingAWriteKey_ThenTheRefusalIsSingular()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var marked = Press(state, "Alt-m").State;

        var result = Chord(marked, "Space i d");

        Assert.Equal("1 issue selected; clear the selection first", result.State.Message);
    }

    [Fact]
    public void GivenAVisualRangeOfSeveralRows_WhenPressingARankToKey_ThenItRefusesWithoutAWrite()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var ranged = Press(Press(state, "v").State, "j").State;

        var result = Chord(ranged, "Space k b");

        Assert.Equal("2 issues selected; clear the selection first", result.State.Message);
        Assert.Empty(result.Effects);
        Assert.Empty(result.State.Session.Operations);
    }

    [Fact]
    public void GivenAMarkWithACount_WhenPressingAWriteKey_ThenTheSetIsStillRefused()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(state, "Alt-m").State;

        var result = Chord(Press(marked, "2").State, "Space i d");

        Assert.Equal("1 issue selected; clear the selection first", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenAnActionFromThePalette_WhenASetIsActive_ThenItRefusesLikeTheKey()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var marked = Press(Press(state, "Alt-m").State, "Alt-m").State;

        var result = _reducer.Reduce(marked, new ActionRequested(ActionIds.IssueDelete, 1, _now));

        Assert.Equal("2 issues selected; clear the selection first", result.State.Message);
        Assert.Equal(InputMode.Normal, result.State.Mode);
    }

    [Fact]
    public void GivenAVisualRangeOfOneRow_WhenPressingAWriteKey_ThenTheWriteActsOnTheCursorRow()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var visual = Press(state, "v").State;

        var result = Press(visual, "Alt-j");

        Assert.IsType<RankRequest>(Assert.IsType<ExecuteWriteEffect>(Assert.Single(result.Effects)).Request);
    }

    [Fact]
    public void GivenAnIssueOpen_WhenMarksExist_ThenAWriteActsOnTheOpenedIssue()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var marked = Press(Press(state, "Alt-m").State, "Alt-m").State;
        var opened = Press(marked, "Enter").State;

        var result = Press(opened, "Alt-j");

        Assert.DoesNotContain("selected", result.State.Message);
    }

    [Fact]
    public void GivenTheColumnsLayout_WhenPressingV_ThenItIsRefusedWithAReason()
    {
        var state = Loaded(ViewLayout.Columns, "A-1", "A-2");

        var visual = Press(state, "v");

        Assert.Equal("Visual mode works in the list and backlog layouts; mark cards with Alt-m", visual.State.Message);
        Assert.Null(Tab(visual.State).VisualAnchor);
    }

    [Fact]
    public void GivenTheColumnsLayout_WhenPressingAltM_ThenTheCardIsMarkedAndTheCursorMovesDownTheColumn()
    {
        var state = Loaded(ViewLayout.Columns, "A-1", "A-2");

        var first = Press(state, "Alt-m");
        var second = Press(first.State, "Alt-m");

        Assert.Equal(["A-1", "A-2"], Tab(second.State).Marks);
        Assert.Equal("A-2", ColumnsNavigation.SelectedIssue(Tab(first.State))?.Key);
    }

    [Fact]
    public void GivenAMarkedCardInTheColumnsLayout_WhenAWriteIsPressed_ThenItRefuses()
    {
        var state = Loaded(ViewLayout.Columns, "A-1", "A-2");
        var marked = Press(state, "Alt-m").State;

        var result = Press(marked, "Alt-l");

        Assert.Equal("1 issue selected; clear the selection first", result.State.Message);
    }

    [Fact]
    public void GivenVisualModeInTheListLayout_WhenSwitchingToColumns_ThenVisualModeEndsAndMarksStay()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var visual = Press(Press(state, "Alt-m").State, "v").State;

        var columns = _reducer.Reduce(visual, new ViewLayoutChanged(Tab(visual).Id, ViewLayout.Columns)).State;

        Assert.Null(Tab(columns).VisualAnchor);
        Assert.Equal(["A-1"], Tab(columns).Marks);
    }

    [Fact]
    public void GivenAnIssueOpen_WhenPressingV_ThenNoRowIsSelectedInTheListUnderIt()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var opened = Press(state, "Enter").State;

        var result = Press(opened, "v");

        Assert.Null(Tab(result.State).VisualAnchor);
        Assert.Equal(0, RowSelection.Count(Tab(result.State)));
    }

    [Fact]
    public void GivenTwoTabs_WhenMarkingInOne_ThenTheOtherKeepsNoMarks()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var marked = Press(state, "Alt-m").State;
        var second = _reducer.Reduce(marked, new ViewOpenRequested(Tab(marked).Definition with { SourceValue = "9" })).State;

        Assert.Empty(Tab(second).Marks);
        Assert.Equal(["A-1"], marked.Session.Tabs[0].Marks);
    }

    [Fact]
    public void GivenAMouseMark_WhenCtrlClickingARow_ThenItIsMarkedInPlaceWithoutMovingDown()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var placed = new PointerPlaced(5, 3, 80, new ActionRequested(ActionIds.SelectionMark, 0, _now));

        var result = _reducer.Reduce(state, placed).State;

        Assert.Equal(["A-2"], Tab(result).Marks);
        Assert.Equal("A-2", Tab(result).SelectedKey);
    }

    [Fact]
    public void GivenTheColumnsLayout_WhenCtrlClickingACard_ThenOnlyThatCardIsMarkedInPlace()
    {
        var state = Loaded(ViewLayout.Columns, "A-1", "A-2");
        var placed = new PointerPlaced(5, 3, 120, new ActionRequested(ActionIds.SelectionMark, 0, _now));

        var result = _reducer.Reduce(state, placed).State;

        Assert.Equal(["A-1"], Tab(result).Marks);
        Assert.Equal("A-1", ColumnsNavigation.SelectedIssue(Tab(result))?.Key);
    }

    [Fact]
    public void GivenTheColumnsLayout_WhenCtrlClickingAColumnHeader_ThenNothingIsMarked()
    {
        var state = Loaded(ViewLayout.Columns, "A-1", "A-2");
        var placed = new PointerPlaced(5, 0, 120, new ActionRequested(ActionIds.SelectionMark, 0, _now));

        var result = _reducer.Reduce(state, placed).State;

        Assert.Empty(Tab(result).Marks);
    }

    [Fact]
    public void GivenADragOverRows_WhenReleasing_ThenVisualModeCoversThemFromPressToRelease()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3", "A-4");

        var result = _reducer.Reduce(state, new RowsDragged(5, 3, 4, 80)).State;

        Assert.Equal(["A-2", "A-3"], RowSelection.Targets(Tab(result)));
        Assert.Equal("A-3", Tab(result).SelectedKey);
    }

    [Fact]
    public void GivenHeldButton_WhenMovingOverRows_ThenTheRangeGrowsAndShrinksWithThePointer()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3", "A-4", "A-5");

        var first = _reducer.Reduce(state, new RowsDragging(5, 3, 3, 80, true)).State;
        var grown = _reducer.Reduce(first, new RowsDragging(5, 3, 5, 80, false)).State;
        var shrunk = _reducer.Reduce(grown, new RowsDragging(5, 3, 4, 80, false)).State;

        Assert.Equal(["A-2"], RowSelection.Targets(Tab(first)));
        Assert.Equal(["A-2", "A-3", "A-4"], RowSelection.Targets(Tab(grown)));
        Assert.Equal(["A-2", "A-3"], RowSelection.Targets(Tab(shrunk)));
    }

    [Fact]
    public void GivenTheFirstMoveAlreadyOnAnotherRow_WhenDragging_ThenTheRangeStartsOnThePressedRow()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3", "A-4");

        var result = _reducer.Reduce(state, new RowsDragging(5, 3, 5, 80, true)).State;

        Assert.Equal("A-2", Tab(result).VisualAnchor);
        Assert.Equal("A-4", Tab(result).SelectedKey);
    }

    [Fact]
    public void GivenALiveRangeScrolledAwayFromThePress_WhenReleasing_ThenTheRangeKeepsItsAnchor()
    {
        var keys = Enumerable.Range(1, 40).Select(number => $"A-{number}").ToArray();
        var state = Loaded(ViewLayout.List, keys);
        var started = _reducer.Reduce(state, new RowsDragging(5, 3, 3, 80, true)).State;
        var scrolled = _reducer.Reduce(started, new RowsDragScrolled(6)).State;
        var again = _reducer.Reduce(scrolled, new RowsDragScrolled(6)).State;

        var released = _reducer.Reduce(again, new RowsDragged(5, 3, 20, 80, _now, true)).State;

        Assert.Equal("A-2", Tab(released).VisualAnchor);
        Assert.Equal("A-14", Tab(again).SelectedKey);
        Assert.Equal(13, RowSelection.Count(Tab(again)));
        Assert.True(RowSelection.Count(Tab(released)) >= 13);
    }

    [Fact]
    public void GivenAutoScrollAtTheEndsOfTheList_WhenStepping_ThenTheCursorStopsAtTheFirstAndLastRow()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var started = _reducer.Reduce(state, new RowsDragging(5, 3, 3, 80, true)).State;

        var down = _reducer.Reduce(started, new RowsDragScrolled(6)).State;
        var up = _reducer.Reduce(down, new RowsDragScrolled(-6)).State;

        Assert.Equal("A-3", Tab(down).SelectedKey);
        Assert.Equal("A-1", Tab(up).SelectedKey);
        Assert.Equal("A-2", Tab(up).VisualAnchor);
    }

    [Fact]
    public void GivenNoVisualRange_WhenAnAutoScrollStepArrives_ThenNothingMoves()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");

        var result = _reducer.Reduce(state, new RowsDragScrolled(2));

        Assert.Same(state, result.State);
    }

    [Fact]
    public void GivenAReleaseWithoutALiveRange_WhenFlaggedLive_ThenItSelectsFromPressToRelease()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3", "A-4");

        var result = _reducer.Reduce(state, new RowsDragged(5, 3, 4, 80, _now, true)).State;

        Assert.Equal(["A-2", "A-3"], RowSelection.Targets(Tab(result)));
    }

    [Fact]
    public void GivenAMarkedRow_WhenCountingTheSelection_ThenTheStatusTextHasSomethingToShow()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(Press(state, "Alt-m").State, "Alt-m").State;

        Assert.Equal(2, RowSelection.Count(Tab(marked)));
        Assert.Equal(0, RowSelection.Count(Tab(Loaded(ViewLayout.List, "A-1"))));
    }

    [Fact]
    public void GivenMarksAndTheColumnsLayout_WhenDraggingACard_ThenTheDropIsRefusedLikeTheKey()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2");
        var marked = Press(state, "Alt-m").State;
        var columns = _reducer.Reduce(marked, new ViewLayoutChanged(Tab(marked).Id, ViewLayout.Columns)).State;

        var result = _reducer.Reduce(columns, new CardDragged(5, 3, 60, 120));

        Assert.Equal("1 issue selected; clear the selection first", result.State.Message);
        Assert.Empty(result.Effects);
    }

    [Fact]
    public void GivenStaleCachedRows_WhenPruning_ThenMarksOfRowsBeyondTheCachedPageStay()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var marked = Press(Press(Press(state, "j").State, "j").State, "Alt-m").State;
        var tab = Tab(marked);
        var cached = tab with { Rows = [.. tab.Rows.Take(1)], IsStale = true };

        var pruned = RowSelection.Pruned(cached);

        Assert.Equal(["A-3"], pruned.Marks);
    }

    [Fact]
    public void GivenVisualModeAndACtrlClick_WhenMarkingInPlace_ThenOnlyTheClickedRowIsMarked()
    {
        var state = Loaded(ViewLayout.List, "A-1", "A-2", "A-3");
        var ranged = Press(Press(state, "v").State, "j").State;
        var placed = new PointerPlaced(5, 4, 80, new ActionRequested(ActionIds.SelectionMark, 0, _now));

        var result = _reducer.Reduce(ranged, placed).State;

        Assert.Equal(["A-3"], Tab(result).Marks);
    }

    private static TabState Tab(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static IssueSummary Row(string key)
    {
        return new IssueSummary(
            key,
            key,
            $"Summary {key}",
            new IssueStatus("1", "Status", "new"),
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

    private AppState Reload(AppState state, params string[] keys)
    {
        var tab = Tab(state);
        var refresh = _reducer.Reduce(state, new RefreshRequested(tab.Id)).State;
        var current = Tab(refresh);
        var rows = keys.Select(Row).ToArray();

        return _reducer.Reduce(
            refresh,
            new ViewPageLoaded(current.Id, current.RequestId, new IssuePage(rows, null, rows.Length), _now)).State;
    }

    private AppState Loaded(ViewLayout layout, params string[] keys)
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
        var viewport = _reducer.Reduce(opened, new ViewportChanged(20)).State;
        var tab = Tab(viewport);
        var rows = keys.Select(Row).ToArray();

        return _reducer.Reduce(
            viewport,
            new ViewPageLoaded(tab.Id, tab.RequestId, new IssuePage(rows, null, rows.Length), _now)).State;
    }
}
