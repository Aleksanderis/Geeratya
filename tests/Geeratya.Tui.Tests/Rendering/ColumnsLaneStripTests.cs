using Geeratya.Core.Boards;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ColumnsLaneStripTests
{
    private const int Width = 100;
    private const int Height = 30;

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
    public void GivenAGrouping_WhenRendering_ThenTheFirstRowListsEveryLaneAndMarksTheCurrentOne()
    {
        var tab = Grouped("u2");

        var body = Render(tab);

        Assert.StartsWith("▸ Al 2  ▾ Bo 1  ▸ Unassigned 1", body[0].PlainText, StringComparison.Ordinal);
        Assert.Equal(TextRole.Heading, body[0].Spans.Single(span => span.Text.StartsWith('▾')).Role);
        Assert.Equal(TextRole.Muted, body[0].Spans.First(span => span.Text.StartsWith('▸')).Role);
        Assert.Equal(Height, body.Count);
    }

    [Fact]
    public void GivenAGrouping_WhenRendering_ThenOnlyTheCurrentLanesCardsAreDrawnBelowTheStrip()
    {
        var body = Render(Grouped("u2"));

        Assert.Contains(body.Skip(1), line => line.PlainText.Contains("A-3", StringComparison.Ordinal));
        Assert.DoesNotContain(body.Skip(1), line => line.PlainText.Contains("A-1", StringComparison.Ordinal));
    }

    [Fact]
    public void GivenNoGrouping_WhenRendering_ThenThereIsNoStripAndTheFrameStartsAtRowZero()
    {
        var tab = Grouped("u2") with { Swimlane = SwimlaneKind.None };

        var body = Render(tab);

        Assert.StartsWith("╭", body[0].PlainText, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenAStrip_WhenClickingAnotherLane_ThenItShowsThatLaneWithoutOpeningAnything()
    {
        var state = Active(Grouped("u1"));
        var layout = LaneStrip.Layout(SwimlaneKind.Assignee, "u1", Rows(), Width);
        var other = layout.Chips.Single(chip => chip.Key == "u2");

        var result = _reducer.Reduce(state, new PointerPlaced(other.Start + 1, 0, Width)).State;
        var tab = result.Session.Tabs.Single();

        Assert.Equal("u2", tab.Lane);
        Assert.Equal("A-3", tab.SelectedKey);
        Assert.Empty(tab.Detail);
    }

    [Fact]
    public void GivenAStrip_WhenClickingACardRow_ThenTheBoardRowsAreShiftedByTheStrip()
    {
        var state = Active(Grouped("u1"));
        var tab = state.Session.Tabs.Single();

        // Body row 0 is the strip, row 1 the frame's top edge, so the first card box starts at row 2.
        Assert.False(ColumnsNavigation.IsCardAt(tab, Width, 5, 1));
        Assert.True(ColumnsNavigation.IsCardAt(tab, Width, 5, 3));
        Assert.Equal("u2", ColumnsNavigation.LaneAt(tab, Width, LaneColumn(tab, "u2"), 0));
        Assert.Null(ColumnsNavigation.LaneAt(tab, Width, LaneColumn(tab, "u2"), 1));
    }

    [Fact]
    public void GivenManyLanes_WhenTheCurrentOneIsLast_ThenTheStripScrollsToShowItWholeWithinTheWidth()
    {
        var rows = Enumerable.Range(0, 12)
            .Select(index => Issue($"B-{index}", "1", $"u{index}", $"Person number {index}"))
            .ToArray();

        var layout = LaneStrip.Layout(SwimlaneKind.Assignee, "u11", rows, 40);

        Assert.True(layout.MoreLeft);
        Assert.False(layout.MoreRight);
        Assert.Equal("u11", layout.Chips[^1].Key);
        Assert.True(layout.Chips[^1].Current);
        Assert.All(layout.Chips, chip => Assert.InRange(chip.End, 1, 40));
    }

    [Fact]
    public void GivenManyLanes_WhenTheCurrentOneIsFirst_ThenLaterLanesAreCutWithAMark()
    {
        var rows = Enumerable.Range(0, 12)
            .Select(index => Issue($"B-{index}", "1", $"u{index}", $"Person number {index}"))
            .ToArray();

        var layout = LaneStrip.Layout(SwimlaneKind.Assignee, "u0", rows, 40);

        Assert.False(layout.MoreLeft);
        Assert.True(layout.MoreRight);
        Assert.True(layout.Chips[0].Current);
        Assert.All(layout.Chips, chip => Assert.InRange(chip.End, 1, 39));
    }

    [Theory]
    [InlineData(20, 5)]
    [InlineData(10, 3)]
    [InlineData(80, 4)]
    public void GivenATinyBody_WhenRenderingWithAStrip_ThenItNeverThrowsAndStaysWithinTheHeight(int width, int height)
    {
        var body = new ColumnsBodyRenderer().Render(Active(Grouped("u1")), new BodyContext(_keymap), width, height);

        Assert.InRange(body.Count, 1, height);
    }

    private static IReadOnlyList<StyledLine> Render(TabState tab)
    {
        return new ColumnsBodyRenderer().Render(Active(tab), new BodyContext(_keymap), Width, Height);
    }

    private static int LaneColumn(TabState tab, string key)
    {
        return LaneStrip.Layout(tab.Swimlane, tab.Lane, tab.Rows, Width).Chips.Single(chip => chip.Key == key).Start;
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static IReadOnlyList<IssueSummary> Rows()
    {
        return
        [
            Issue("A-1", "1", "u1", "Al"),
            Issue("A-2", "1", "u1", "Al"),
            Issue("A-3", "1", "u2", "Bo"),
            Issue("A-4", "4", null, null),
        ];
    }

    private static TabState Grouped(string lane)
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, Height - 3);
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
            Lane = lane,
        });
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
