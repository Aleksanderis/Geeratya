using Geeratya.Core.Boards;
using Geeratya.Core.Flash;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

[Trait("Os", "Independent")]
public class ColumnsStackedLanesTests
{
    private const int Width = 100;
    private const int Height = 40;

    private static readonly KeymapTable _keymap = DefaultKeymap.Load();

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
    public void GivenStackedLanes_WhenRendering_ThenEachLaneHasAHeaderAndItsOwnFrameAtTheRegionRows()
    {
        var tab = Stacked();
        var body = Render(tab);
        var regions = LaneRegions.Regions(tab);

        Assert.Equal(Height, body.Count);

        foreach (var region in regions)
        {
            Assert.StartsWith(region.Key == "u1" ? "▾ " : "▸ ", body[region.Top].PlainText, StringComparison.Ordinal);
            Assert.StartsWith("╭", body[region.BoardTop].PlainText, StringComparison.Ordinal);
            Assert.StartsWith("╰", body[region.Top + region.Rows - 1].PlainText, StringComparison.Ordinal);
        }

        Assert.Equal(TextRole.Heading, body[regions[0].Top].Spans[0].Role);
        Assert.Equal(TextRole.Muted, body[regions[1].Top].Spans[0].Role);
    }

    [Fact]
    public void GivenStackedLanes_WhenRendering_ThenEveryLanesCardsAreDrawnUnderItsOwnHeader()
    {
        var tab = Stacked();
        var body = Render(tab);

        foreach (var region in LaneRegions.Regions(tab))
        {
            var lane = string.Join('\n', body.Skip(region.Top).Take(region.Rows).Select(line => line.PlainText));
            var prefix = region.Key == "u1" ? "A-" : region.Key == "u2" ? "B-" : "C-";

            Assert.Contains($"{prefix}1", lane, StringComparison.Ordinal);
        }

        var all = string.Join('\n', body.Select(line => line.PlainText));

        Assert.Equal(1, Count(all, "A-1 "));
        Assert.Equal(1, Count(all, "C-2 "));
    }

    [Fact]
    public void GivenStackedLanes_WhenRendering_ThenOnlyTheCurrentLanesCardIsSelected()
    {
        var current = Render(Stacked());
        var other = Render(ColumnsNavigation.SwitchLane(Stacked(), "u2"));

        Assert.Contains(current, line => line.Spans.Any(span => span.Ground == SpanGround.Selection));
        Assert.Contains(other, line => line.Spans.Any(span => span.Ground == SpanGround.Selection));
        Assert.Equal(SelectedLines(current).Count, SelectedLines(other).Count);
        Assert.All(SelectedLines(current), row => Assert.InRange(row, 1, 13));
        Assert.All(SelectedLines(other), row => Assert.InRange(row, 15, 26));
    }

    [Fact]
    public void GivenStackedLanes_WhenAskingWhereCardsAreDrawn_ThenTheHitTestFindsTheCardOnEveryRowItIsDrawn()
    {
        var tab = Stacked();
        var body = Render(tab);

        for (var row = 0; row < body.Count; row++)
        {
            var text = body[row].PlainText;
            var column = text.IndexOf("B-1", StringComparison.Ordinal);

            if (column >= 0)
            {
                Assert.True(ColumnsNavigation.IsCardAt(tab, Width, column, row), $"row {row}");
                Assert.Equal("B-1", ColumnsNavigation.Place(tab, Width, column, row).SelectedKey);
            }
        }
    }

    [Fact]
    public void GivenATallColumnInAStackedLane_WhenMovingDownPastItsShare_ThenTheSelectedCardScrollsIntoView()
    {
        var rows = Enumerable.Range(0, 14).Select(index => Issue($"T-{index}", "1", "u1", "Al"))
            .Concat([Issue("B-1", "1", "u2", "Bo")])
            .ToArray();
        var tab = ColumnsNavigation.Resync(Stacked() with { Rows = rows, SelectedKey = "T-0" });

        for (var press = 0; press < 12; press++)
        {
            tab = ColumnsNavigation.Move(tab, ListMove.Down);
        }

        var body = Render(tab);
        var region = LaneRegions.FocusRegion(tab);
        var lane = string.Join('\n', body.Skip(region.Top).Take(region.Rows).Select(line => line.PlainText));

        Assert.Equal("T-12", tab.SelectedKey);
        Assert.Contains("T-12", lane, StringComparison.Ordinal);
        Assert.Equal(Height, body.Count);
    }

    [Fact]
    public void GivenStackedLanes_WhenLabellingForFlash_ThenTheLabelsAreCardsDrawnSomewhereInTheBody()
    {
        var tab = Stacked();
        var body = string.Join('\n', Render(tab).Select(line => line.PlainText));

        var labels = FlashBoardLabeler.Compute(tab, "Summary", Width, Height);

        Assert.NotEmpty(labels);
        Assert.All(labels, label => Assert.Contains(label.Key!, body, StringComparison.Ordinal));
        Assert.Equal(8, labels.Count);
    }

    [Theory]
    [InlineData(20, 5)]
    [InlineData(10, 3)]
    [InlineData(80, 4)]
    [InlineData(30, 12)]
    [InlineData(60, 20)]
    [InlineData(120, 60)]
    public void GivenAnyBodySize_WhenRenderingStackedLanes_ThenItNeverThrowsAndFillsAtMostTheHeight(int width, int height)
    {
        var tab = Stacked() with { VisibleRows = Math.Max(0, height - 3) };
        var body = new ColumnsBodyRenderer().Render(Active(tab), new BodyContext(_keymap), width, height);

        Assert.InRange(body.Count, 1, Math.Max(height, 1));
    }

    private static List<int> SelectedLines(IReadOnlyList<StyledLine> body)
    {
        return [.. Enumerable.Range(0, body.Count).Where(row => body[row].Spans.Any(span => span.Ground == SpanGround.Selection))];
    }

    private static int Count(string text, string needle)
    {
        return text.Split(needle).Length - 1;
    }

    private static IReadOnlyList<StyledLine> Render(TabState tab)
    {
        return new ColumnsBodyRenderer().Render(Active(tab), new BodyContext(_keymap), Width, Height);
    }

    private static AppState Active(TabState tab)
    {
        var profile = new Profile("work", "https://work.atlassian.net", "me@work.test");
        var session = SiteSession.Empty(profile) with { Tabs = [tab], Active = tab.Id };

        return AppState.Initial with { Profiles = ProfileCatalog.Empty.With(profile), Session = session };
    }

    private static TabState Stacked()
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
            Issue("A-3", "4", "u1", "Al"),
            Issue("B-1", "1", "u2", "Bo"),
            Issue("B-2", "1", "u2", "Bo"),
            Issue("B-3", "4", "u2", "Bo"),
            Issue("C-1", "1", "u3", "Cy"),
            Issue("C-2", "4", "u3", "Cy"),
        ];
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
