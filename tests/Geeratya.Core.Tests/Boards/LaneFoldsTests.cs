using Geeratya.Core.Boards;
using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class LaneFoldsTests
{
    private const int Width = 100;
    private const int VisibleRows = 37;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly AppReducer _reducer = new(_keymap);

    private static readonly BoardConfiguration _configuration = new(
        4,
        "Board",
        BoardType.Kanban,
        [new BoardColumn("To Do", ["1"]), new BoardColumn("Done", ["4"])],
        "10001",
        null,
        null,
        null);

    [Fact]
    public void GivenStackedLanes_WhenFoldingTheCurrentLane_ThenItShrinksToItsHeaderAndTheNextOpenLaneIsCurrent()
    {
        var folded = Reduce(Stacked(), ActionIds.LaneFold);
        var tab = Only(folded);
        var regions = LaneRegions.Regions(tab);

        Assert.Equal("u2", tab.Lane);
        Assert.Equal(["u1"], tab.FoldedLanes);
        Assert.Equal(1, regions[0].Rows);
        Assert.True(regions[0].Folded);
        Assert.False(regions[1].Folded);
        Assert.Equal("B-1", tab.SelectedKey);
        Assert.Equal("Folded lane Al.", folded.Message);
    }

    [Fact]
    public void GivenAFoldedLane_WhenUnfolding_ThenTheLaneOpensAndBecomesCurrentWithItsRowsBack()
    {
        var folded = Reduce(Stacked(), ActionIds.LaneFold);

        var opened = Only(Reduce(folded, ActionIds.LaneUnfold));

        Assert.Equal("u1", opened.Lane);
        Assert.Empty(opened.FoldedLanes);
        Assert.All(LaneRegions.Regions(opened), region => Assert.False(region.Folded));
        Assert.Equal([14, 13, 13], LaneRegions.Regions(opened).Select(region => region.Rows));
    }

    [Fact]
    public void GivenNoFoldedLane_WhenUnfolding_ThenItSaysSo()
    {
        var result = Reduce(Stacked(), ActionIds.LaneUnfold);

        Assert.Equal(LaneFolds.NoneFoldedText, result.Message);
    }

    [Fact]
    public void GivenLanesThatAreNotStacked_WhenFolding_ThenItIsRefusedWithAHint()
    {
        var result = Reduce(Stacked() with { StackLanes = false }, ActionIds.LaneFold);

        Assert.Equal(LaneFolds.NotStackedText, result.Message);
        Assert.Empty(Only(result).FoldedLanes);
    }

    [Fact]
    public void GivenAllOtherLanesFolded_WhenFoldingTheLastOpenLane_ThenItIsRefused()
    {
        var first = Reduce(Stacked(), ActionIds.LaneFold);
        var second = Reduce(first, ActionIds.LaneFold);

        var third = Reduce(second, ActionIds.LaneFold);

        Assert.Equal(LaneFolds.LastOpenText, third.Message);
        Assert.Equal(["u1", "u2"], Only(third).FoldedLanes);
        Assert.Equal("u3", Only(third).Lane);
    }

    [Fact]
    public void GivenFoldedLanes_WhenLayingOutTheBody_ThenFoldedLanesTakeOneRowAndTheOpenLanesShareTheRest()
    {
        var tab = Stacked() with { FoldedLanes = ["u1", "u3"], Lane = "u2" };

        var regions = LaneRegions.Regions(tab);

        Assert.Equal([0, 1, 39], regions.Select(region => region.Top));
        Assert.Equal([1, 38, 1], regions.Select(region => region.Rows));
        Assert.Equal([true, false, true], regions.Select(region => region.Folded));
        Assert.Equal(LaneRegions.FocusRegion(tab), regions[1]);
    }

    [Fact]
    public void GivenTheCurrentLaneIsInTheFoldedList_WhenLayingOut_ThenItIsDrawnOpen()
    {
        var tab = Stacked() with { FoldedLanes = ["u1"] };

        Assert.False(LaneRegions.IsFolded(tab, "u1"));
        Assert.All(LaneRegions.Regions(tab), region => Assert.False(region.Folded));
    }

    [Fact]
    public void GivenAFoldedLane_WhenStepping_ThenTheLaneOpensAndIsCurrent()
    {
        var folded = Reduce(Stacked(), ActionIds.LaneFold);
        var onThird = Only(folded) with { Lane = "u3" };
        var state = Active(onThird);

        var stepped = Only(_reducer.Reduce(state, new ActionRequested(ActionIds.SectionNext)).State);

        Assert.Equal("u1", stepped.Lane);
        Assert.Empty(stepped.FoldedLanes);
    }

    [Fact]
    public void GivenAFoldedLane_WhenFlashLabelling_ThenOnlyOpenLanesGetLabels()
    {
        var tab = Only(Reduce(Stacked(), ActionIds.LaneFold));

        var labels = FlashBoardLabeler.Compute(tab, "Summary", Width, VisibleRows + 3);

        Assert.DoesNotContain(labels, label => label.Key!.StartsWith("A-", StringComparison.Ordinal));
        Assert.Contains(labels, label => label.Key == "B-1");
        Assert.Contains(labels, label => label.Key == "C-1");
    }

    [Fact]
    public void GivenAFoldedLane_WhenAskingForCards_ThenItsHeaderIsNotACardAndTheOpenLanesStillHit()
    {
        var tab = Only(Reduce(Stacked(), ActionIds.LaneFold));
        var regions = LaneRegions.Regions(tab);

        Assert.False(ColumnsNavigation.IsCardAt(tab, Width, 5, regions[0].Top));
        Assert.True(ColumnsNavigation.IsLaneHeaderAt(tab, regions[0].Top));
        Assert.False(ColumnsNavigation.IsLaneHeaderAt(tab, regions[1].Top + 1));
    }

    [Fact]
    public void GivenAClickOnTheChevronOfAnOpenLaneHeader_WhenPlacing_ThenThatLaneFoldsAndTheCursorStays()
    {
        var tab = Stacked();
        var third = LaneRegions.Regions(tab)[2];

        var placed = ViewReducer.PlaceCursor(Active(tab), new PointerPlaced(0, third.Top, Width));

        var result = Only(placed!.State);

        Assert.Equal(["u3"], result.FoldedLanes);
        Assert.Equal("u1", result.Lane);
        Assert.Equal("A-1", result.SelectedKey);
        Assert.True(LaneRegions.Regions(result)[2].Folded);
    }

    [Fact]
    public void GivenAClickOnAFoldedLaneHeader_WhenPlacing_ThenItOpensAndBecomesCurrent()
    {
        var folded = Only(Reduce(Stacked(), ActionIds.LaneFold));
        var header = LaneRegions.Regions(folded)[0];

        var placed = ViewReducer.PlaceCursor(Active(folded), new PointerPlaced(10, header.Top, Width));

        var result = Only(placed!.State);

        Assert.Equal("u1", result.Lane);
        Assert.Empty(result.FoldedLanes);
    }

    [Fact]
    public void GivenAClickOnTheNameOfAnOpenLaneHeader_WhenPlacing_ThenTheLaneIsOnlyFocusedNotFolded()
    {
        var tab = Stacked();
        var second = LaneRegions.Regions(tab)[1];

        var placed = ViewReducer.PlaceCursor(Active(tab), new PointerPlaced(10, second.Top, Width));

        var result = Only(placed!.State);

        Assert.Equal("u2", result.Lane);
        Assert.Empty(result.FoldedLanes);
    }

    [Fact]
    public void GivenFoldedLanes_WhenTheGroupingIsCycled_ThenTheFoldsAreForgotten()
    {
        var folded = Reduce(Stacked(), ActionIds.LaneFold);

        var cycled = Reduce(folded, ActionIds.ViewSwimlane);

        Assert.Empty(Only(cycled).FoldedLanes);
    }

    [Fact]
    public void GivenTheZcAndZoKeys_WhenPressed_ThenTheLaneFoldsAndOpensThroughTheKeymap()
    {
        var folded = _reducer.Reduce(_reducer.Reduce(Active(Stacked()), Key('z')).State, Key('c')).State;

        Assert.Equal(["u1"], Only(folded).FoldedLanes);

        var opened = _reducer.Reduce(_reducer.Reduce(folded, Key('z')).State, Key('o')).State;

        Assert.Empty(Only(opened).FoldedLanes);
    }

    [Fact]
    public void GivenARightClickOrDoubleClickOnALaneHeader_WhenPointing_ThenNoCardActionRuns()
    {
        var tab = Stacked();
        var second = LaneRegions.Regions(tab)[1];

        foreach (var action in new[] { ActionIds.ItemOpen, ActionIds.ContextMenuOpen })
        {
            var then = new ActionRequested(action, 1, DateTimeOffset.UnixEpoch);

            var result = _reducer.Reduce(Active(tab), new PointerPlaced(10, second.Top, Width, then));

            Assert.Equal("u2", Only(result.State).Lane);
            Assert.Equal(InputMode.Normal, result.State.Mode);
            Assert.Empty(Only(result.State).Detail);
            Assert.Null(result.State.ContextMenu);
        }
    }

    private static KeyPressed Key(char key)
    {
        return new KeyPressed(key.ToString(), DateTimeOffset.UnixEpoch);
    }

    private static AppState Reduce(AppState state, string action)
    {
        return _reducer.Reduce(state, new ActionRequested(action)).State;
    }

    private static AppState Reduce(TabState tab, string action)
    {
        return Reduce(Active(tab), action);
    }

    private static TabState Only(AppState state)
    {
        return state.Session.Tabs.Single();
    }

    private static TabState Stacked()
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, VisibleRows);
        var rows = Rows();

        return ColumnsNavigation.Resync(opened with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = _configuration,
            SelectedKey = rows[0].Key,
            KeyWidth = 3,
            ViewWidth = Width,
            Swimlane = SwimlaneKind.Assignee,
            Lane = "u1",
            StackLanes = true,
        });
    }

    private static IReadOnlyList<IssueSummary> Rows()
    {
        return
        [
            Issue("A-1", "1", "u1", "Al"),
            Issue("A-2", "1", "u1", "Al"),
            Issue("B-1", "1", "u2", "Bo"),
            Issue("B-2", "4", "u2", "Bo"),
            Issue("C-1", "1", "u3", "Cy"),
            Issue("C-2", "4", "u3", "Cy"),
        ];
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IssueSummary Issue(string key, string statusId, string? account, string? name)
    {
        return new IssueSummary(
            key,
            key,
            "Summary",
            new IssueStatus(statusId, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            account is null ? null : new IssueAssignee(account, name!),
            [],
            null,
            null,
            "A");
    }
}
