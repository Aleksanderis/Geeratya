using Geeratya.Core.Boards;
using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class LaneRegionsTests
{
    private const int Width = 100;

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
    public void GivenThreeLanesAndRoom_WhenStacked_ThenTheBodyUnderTheStripIsSplitEquallyWithAHeaderEach()
    {
        var regions = LaneRegions.Regions(Stacked(37));

        Assert.Equal(["u1", "u2", "u3"], regions.Select(region => region.Key));
        Assert.Equal([0, 14, 27], regions.Select(region => region.Top));
        Assert.Equal([14, 13, 13], regions.Select(region => region.Rows));
        Assert.All(regions, region => Assert.True(region.HasHeader));
        Assert.Equal(10, regions[0].BoardRows);
        Assert.Equal(1, regions[0].BoardTop);
    }

    [Fact]
    public void GivenAnUnevenSplit_WhenStacked_ThenTheFirstLanesTakeTheRemainderAndNothingIsLost()
    {
        var regions = LaneRegions.Regions(Stacked(38));

        Assert.Equal([14, 14, 13], regions.Select(region => region.Rows));
        Assert.Equal(41, regions.Sum(region => region.Rows));
        Assert.Equal(regions[1].Top, regions[0].Top + regions[0].Rows);
    }

    [Fact]
    public void GivenEveryLaneDrawnWithItsOwnHeader_WhenStacked_ThenTheStripIsLeftOutAndItsRowGoesToTheLanes()
    {
        var tab = Stacked(37);

        Assert.True(LaneRegions.StripHidden(tab));
        Assert.False(LaneStrip.IsDrawn(tab));
        Assert.Null(ColumnsNavigation.LaneAt(tab, Width, 3, 0));
        Assert.Equal(40, LaneRegions.Regions(tab).Sum(region => region.Rows));
    }

    [Fact]
    public void GivenSomeLanesNotDrawn_WhenStacked_ThenTheStripStaysAsTheListOfAllOfThem()
    {
        var tab = Stacked(21, 5);

        Assert.False(LaneRegions.StripHidden(tab));
        Assert.True(LaneStrip.IsDrawn(tab));
        Assert.Equal(LaneStrip.Rows, LaneRegions.Regions(tab)[0].Top);

        var second = LaneStrip.Layout(ColumnsNavigation.LanesOf(tab), tab.Lane, Width).Chips[1];

        Assert.Equal("u1", ColumnsNavigation.LaneAt(tab, Width, second.Start, 0));
    }

    [Fact]
    public void GivenStackingOff_WhenAskingForRegions_ThenOneLaneTakesTheBodyUnderTheStripAsBefore()
    {
        var tab = Stacked(37) with { StackLanes = false };
        var regions = LaneRegions.Regions(tab);

        Assert.False(LaneRegions.IsStacked(tab));
        Assert.Single(regions);
        Assert.False(regions[0].HasHeader);
        Assert.Equal(LaneStrip.Rows, regions[0].Top);
        Assert.Equal(tab.VisibleRows - LaneStrip.Rows, LaneStrip.BoardRows(tab));
    }

    [Fact]
    public void GivenABodyTooShortForTwoLanes_WhenStacked_ThenItFallsBackToTheCurrentLaneAlone()
    {
        var tab = Stacked(9);

        Assert.False(LaneRegions.IsStacked(tab));
        Assert.False(LaneRegions.FocusRegion(tab).HasHeader);
    }

    [Fact]
    public void GivenMoreLanesThanFit_WhenTheCurrentLaneChanges_ThenThePagesStayFixedAndTheLastEndsAtTheLastLane()
    {
        var tab = Stacked(21, 5);

        Assert.Equal(["u0", "u1"], Keys(tab, "u0"));
        Assert.Equal(["u0", "u1"], Keys(tab, "u1"));
        Assert.Equal(["u2", "u3"], Keys(tab, "u2"));
        Assert.Equal(["u3", "u4"], Keys(tab, "u4"));
    }

    [Fact]
    public void GivenStackedLanes_WhenAskingForTheScrollRows_ThenTheCurrentLanesShareIsUsedNotTheWholeBody()
    {
        var tab = Stacked(37);

        Assert.Equal(10, LaneStrip.BoardRows(tab));
        Assert.Equal(10, ColumnsNavigation.ViewOf(tab).VisibleRows);
        Assert.Equal(1, LaneStrip.Offset(tab));
    }

    [Fact]
    public void GivenALaneLeft_WhenComingBack_ThenItsOwnCardAndColumnAreSelectedAgain()
    {
        var tab = ColumnsNavigation.Move(Stacked(37), ListMove.Down);

        Assert.Equal("A-2", tab.SelectedKey);

        var other = ColumnsNavigation.SwitchLane(tab, "u2");

        Assert.Equal("u2", other.Lane);
        Assert.Equal("B-1", other.SelectedKey);

        other = ColumnsNavigation.Move(other, ListMove.Down);

        Assert.Equal("B-2", other.SelectedKey);

        var back = ColumnsNavigation.SwitchLane(other, "u1");

        Assert.Equal("A-2", back.SelectedKey);
        Assert.Equal("B-2", ColumnsNavigation.SwitchLane(back, "u2").SelectedKey);
    }

    [Fact]
    public void GivenAColumnOtherThanTheFirst_WhenSwitchingLane_ThenTheColumnStaysSoEveryLaneShowsTheSameWindow()
    {
        var tab = ColumnsNavigation.MoveColumn(Stacked(37), BoardColumnMove.Right);

        var other = ColumnsNavigation.SwitchLane(tab, "u2");

        Assert.Equal(1, other.Cursor!.Column);
        Assert.Equal("B-3", other.SelectedKey);
    }

    [Fact]
    public void GivenStackingOff_WhenSwitchingLane_ThenTheCursorGoesToTheFirstCardOfTheLaneAsBefore()
    {
        var tab = ColumnsNavigation.MoveColumn(Stacked(37) with { StackLanes = false }, BoardColumnMove.Right);

        var other = ColumnsNavigation.SwitchLane(tab, "u2");

        Assert.Equal("B-1", other.SelectedKey);
        Assert.Equal(0, other.Cursor!.Column);
        Assert.Empty(other.LaneCursors);
    }

    [Fact]
    public void GivenAClickOnACardOfAnotherLane_WhenPlacing_ThenThatLaneBecomesCurrentWithThatCardSelected()
    {
        var tab = Stacked(37);
        var region = LaneRegions.Regions(tab)[1];

        Assert.True(ColumnsNavigation.IsCardAt(tab, Width, 5, region.BoardTop + 2));

        var placed = ColumnsNavigation.Place(tab, Width, 5, region.BoardTop + 2);

        Assert.Equal("u2", placed.Lane);
        Assert.Equal("B-1", placed.SelectedKey);
    }

    [Fact]
    public void GivenAClickOnALaneHeaderRow_WhenPlacing_ThenThatLaneBecomesCurrent()
    {
        var tab = Stacked(37);
        var region = LaneRegions.Regions(tab)[2];

        var placed = ColumnsNavigation.Place(tab, Width, 5, region.Top);

        Assert.Equal("u3", placed.Lane);
        Assert.False(ColumnsNavigation.IsCardAt(tab, Width, 5, region.Top));
    }

    [Fact]
    public void GivenAClickBelowTheLastLane_WhenPlacing_ThenNothingChanges()
    {
        var tab = Stacked(37);

        Assert.Same(tab, ColumnsNavigation.Place(tab, Width, 5, 60));
        Assert.False(ColumnsNavigation.IsCardAt(tab, Width, 5, 60));
    }

    [Fact]
    public void GivenALaneThatIsNotCurrent_WhenADragStartsOnItsCard_ThenNoDropIsOffered()
    {
        var tab = Stacked(37);
        var region = LaneRegions.Regions(tab)[1];

        Assert.Null(ColumnsNavigation.DragSteps(tab, Width, 5, region.BoardTop + 2, 70));
    }

    [Fact]
    public void GivenStackedLanes_WhenLabellingForFlash_ThenEveryDrawnLaneGetsLabelsAndTheCurrentOneComesFirst()
    {
        var tab = ColumnsNavigation.SwitchLane(Stacked(37), "u2");

        var labels = FlashBoardLabeler.Compute(tab, "Summary", Width, 40);

        Assert.StartsWith("B-", labels[0].Key, StringComparison.Ordinal);
        Assert.Contains(labels, label => label.Key == "A-1");
        Assert.Contains(labels, label => label.Key == "C-1");
    }

    [Fact]
    public void GivenAFlashJumpToACardOfAnotherLane_WhenSelecting_ThenThatLaneBecomesCurrent()
    {
        var tab = ColumnsNavigation.Select(Stacked(37), "C-2");

        Assert.Equal("u3", tab.Lane);
        Assert.Equal("C-2", tab.SelectedKey);
    }

    [Fact]
    public void GivenNoGrouping_WhenTogglingStackedLanes_ThenItIsRefusedWithAHint()
    {
        var state = Active(Grouped(37) with { Swimlane = SwimlaneKind.None });

        var result = _reducer.Reduce(state, new ActionRequested(ActionIds.ViewSwimlaneStack));

        Assert.Equal(BoardViewOptions.NoGroupingText, result.State.Message);
        Assert.False(result.State.Session.Tabs.Single().StackLanes);
    }

    [Fact]
    public void GivenAGrouping_WhenTogglingStackedLanesTwice_ThenTheyOpenAndCloseAndTheCursorStaysOnItsCard()
    {
        var state = Active(Grouped(37));

        var on = _reducer.Reduce(state, new ActionRequested(ActionIds.ViewSwimlaneStack)).State;
        var tab = on.Session.Tabs.Single();

        Assert.True(tab.StackLanes);
        Assert.True(LaneRegions.IsStacked(tab));
        Assert.Contains("3 lanes open", on.Message, StringComparison.Ordinal);
        Assert.Equal("A-1", tab.SelectedKey);

        var off = _reducer.Reduce(on, new ActionRequested(ActionIds.ViewSwimlaneStack)).State;

        Assert.False(off.Session.Tabs.Single().StackLanes);
        Assert.Contains("off", off.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAShortBody_WhenTogglingStackedLanes_ThenTheMessageSaysOnlyOneLaneFits()
    {
        var state = Active(Grouped(9));

        var on = _reducer.Reduce(state, new ActionRequested(ActionIds.ViewSwimlaneStack)).State;

        Assert.Contains("too short", on.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenStackedLanes_WhenTheGroupingIsCycledOff_ThenStackingAndLaneCursorsAreDropped()
    {
        var other = ColumnsNavigation.SwitchLane(Stacked(37), "u2");
        var state = Active(other);

        Assert.NotEmpty(other.LaneCursors);

        var epic = _reducer.Reduce(state, new ActionRequested(ActionIds.ViewSwimlane));
        var none = _reducer.Reduce(epic.State, new ActionRequested(ActionIds.ViewSwimlane)).State.Session.Tabs.Single();

        Assert.False(none.StackLanes);
        Assert.Empty(none.LaneCursors);
    }

    [Fact]
    public void GivenStackedLanes_WhenRememberingTabs_ThenTheFlagRoundTripsOnlyWithAGrouping()
    {
        var state = Active(Stacked(37));
        var remembered = RememberedTabs.Of(state.Session);

        Assert.True(remembered.Items[0].Stacked);
        Assert.True(remembered.ToRestored().Tabs[0].Stacked);

        var ungrouped = Active(Stacked(37) with { Swimlane = SwimlaneKind.None });

        Assert.Null(RememberedTabs.Of(ungrouped.Session).Items[0].Stacked);
    }

    private static IReadOnlyList<string> Keys(TabState tab, string lane)
    {
        return [.. LaneRegions.Regions(tab with { Lane = lane }).Select(region => region.Key)];
    }

    private static TabState Stacked(int visibleRows, int lanes = 3)
    {
        return Grouped(visibleRows, lanes) with { StackLanes = true };
    }

    private static TabState Grouped(int visibleRows, int lanes = 3)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, visibleRows);
        var rows = lanes == 3 ? ThreeLanes() : ManyLanes(lanes);

        return ColumnsNavigation.Resync(opened with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = _configuration,
            SelectedKey = rows[0].Key,
            KeyWidth = 3,
            ViewWidth = Width,
            Swimlane = SwimlaneKind.Assignee,
            Lane = lanes == 3 ? "u1" : "u0",
        });
    }

    private static IReadOnlyList<IssueSummary> ThreeLanes()
    {
        return
        [
            Issue("A-1", "1", "u1", "Al"),
            Issue("A-2", "1", "u1", "Al"),
            Issue("A-3", "4", "u1", "Al"),
            Issue("B-1", "1", "u2", "Bo"),
            Issue("B-2", "1", "u2", "Bo"),
            Issue("B-3", "4", "u2", "Bo"),
            Issue("C-1", "1", "u3", "Cy"),
            Issue("C-2", "4", "u3", "Cy"),
        ];
    }

    private static IReadOnlyList<IssueSummary> ManyLanes(int lanes)
    {
        return [.. Enumerable.Range(0, lanes).Select(index => Issue($"M-{index}", "1", $"u{index}", $"P{index}"))];
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
