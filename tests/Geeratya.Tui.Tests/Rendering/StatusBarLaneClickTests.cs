using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class StatusBarLaneClickTests
{
    private const int Width = 200;
    private const int Height = 30;
    private const string FirstLane = "acc";
    private const string SecondLane = "other";

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GivenALane_WhenClickingTheLaneText_ThenTheNextLaneShows()
    {
        var state = Lanes(FirstLane);
        var cells = StatusBar.LaneCells(state, Width, _keymap, _now)!.Value;
        var line = StatusBar.Render(state, Width, _keymap, _now);

        Assert.Equal("Lane 1/2: Al Ek", line.Substring(cells.Start, cells.End - cells.Start));
        Assert.Equal(SecondLane, Active(Click(state, cells.Start)).Lane);
    }

    [Fact]
    public void GivenANarrowLineThatCutsTheLaneName_WhenClickingTheShownPart_ThenTheNextLaneShows()
    {
        var state = Lanes(FirstLane);
        var line = StatusBar.Render(state, 132, _keymap, _now);
        var cells = StatusBar.LaneCells(state, 132, _keymap, _now)!.Value;

        Assert.Contains("Lane 1/2: A…", line);
        Assert.True(cells.End <= line.IndexOf('…'));
        Assert.Equal(line.IndexOf("Lane 1/2:", StringComparison.Ordinal), cells.Start);
    }

    [Fact]
    public void GivenTheLastLane_WhenClickingTheLaneText_ThenItWrapsToTheFirst()
    {
        var state = Lanes(SecondLane);
        var cells = StatusBar.LaneCells(state, Width, _keymap, _now)!.Value;

        Assert.Equal(FirstLane, Active(Click(state, cells.End - 1)).Lane);
    }

    [Fact]
    public void GivenALane_WhenClickingJustOutsideTheLaneText_ThenTheLaneStays()
    {
        var state = Lanes(FirstLane);
        var cells = StatusBar.LaneCells(state, Width, _keymap, _now)!.Value;

        Assert.Equal(FirstLane, Active(Click(state, cells.Start - 1)).Lane);
        Assert.Equal(FirstLane, Active(Click(state, cells.End)).Lane);
    }

    [Fact]
    public void GivenNoSwimlanes_WhenAskingForTheLaneCells_ThenThereAreNone()
    {
        var state = Lanes(FirstLane);
        var plain = state with { Session = state.Session with { Tabs = [Active(state) with { Swimlane = SwimlaneKind.None }] } };

        Assert.Null(StatusBar.LaneCells(plain, Width, _keymap, _now));
    }

    [Fact]
    public void GivenAMessageOrAnOpenOverlay_WhenAskingForTheLaneCells_ThenThereAreNone()
    {
        var state = Lanes(FirstLane);
        var message = state with { Message = "Saved", Status = new StatusMessage("Saved", StatusSeverity.Info) };

        Assert.Null(StatusBar.LaneCells(message, Width, _keymap, _now));
        Assert.Null(StatusBar.LaneCells(state with { Mode = InputMode.Help }, Width, _keymap, _now));
    }

    private static AppState Click(AppState state, int column)
    {
        var context = MouseContext.From(state, Width, Height, FrameBuilder.HeaderRows, _now, null, _keymap) with
        {
            LaneChip = StatusBar.LaneCells(state, Width, _keymap, _now),
        };
        var reducer = new AppReducer(_keymap);
        var current = state;

        foreach (var command in MouseResolver.Resolve(context, column, Height - 1, MouseGesture.Click, _now))
        {
            current = reducer.Reduce(current, command).State;
        }

        return current;
    }

    private static TabState Active(AppState state)
    {
        return state.Session.Tabs.Single(tab => tab.Id == state.Session.Active);
    }

    private static AppState Lanes(string lane)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, 10);
        var board = new BoardConfiguration(
            4,
            "Board",
            BoardType.Kanban,
            [new BoardColumn("To Do", ["1"])],
            "10001",
            null,
            null,
            null);
        var tab = opened with
        {
            Load = LoadState.Idle,
            Rows = [Issue("A-1", FirstLane, "Al Ek"), Issue("A-2", SecondLane, "Bo Li")],
            Board = board,
            SelectedKey = "A-1",
            KeyWidth = 3,
            Swimlane = SwimlaneKind.Assignee,
            Lane = lane,
        };
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IssueSummary Issue(string key, string account, string name)
    {
        return new IssueSummary(
            key,
            key,
            "Summary",
            new IssueStatus("1", "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            new IssueAssignee(account, name),
            [],
            null,
            null,
            "A");
    }
}
