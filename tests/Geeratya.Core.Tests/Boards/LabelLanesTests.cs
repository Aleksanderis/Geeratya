using Geeratya.Core.Boards;
using Geeratya.Core.Flash;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class LabelLanesTests
{
    private const int Width = 100;

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
    public void GivenACardInTwoLanes_WhenStackedByLabels_ThenEachLaneListsItAndTheUnlabelledLaneIsLast()
    {
        var tab = Grouped();

        Assert.Equal(["ui", "api", ""], ColumnsNavigation.LanesOf(tab).Select(lane => lane.Key));
        Assert.Equal(["X-1", "X-2"], Keys(ColumnsNavigation.ColumnsOfLane(tab, "ui")));
        Assert.Equal(["X-1", "X-3"], Keys(ColumnsNavigation.ColumnsOfLane(tab, "api")));
        Assert.Equal(["X-4"], Keys(ColumnsNavigation.ColumnsOfLane(tab, "")));
    }

    [Fact]
    public void GivenACardInTwoLanes_WhenSteppingToTheOtherLaneAndBack_ThenEachLaneKeepsItsCursor()
    {
        var tab = ColumnsNavigation.Select(Grouped(), "X-2");
        var api = ColumnsNavigation.SwitchLane(tab, "api");

        Assert.Equal("X-1", ColumnsNavigation.SelectedIssue(api)?.Key);

        var back = ColumnsNavigation.SwitchLane(api, "ui");

        Assert.Equal("X-2", ColumnsNavigation.SelectedIssue(back)?.Key);
    }

    [Fact]
    public void GivenALabelDrawnInTheNonCurrentLaneOfASharedCard_WhenJumping_ThenThatLaneBecomesCurrent()
    {
        var tab = Grouped();
        var label = FlashBoardLabeler.Compute(tab, "X-1", Width, 40).Single(found => found.Lane == "api");

        var moved = ColumnsNavigation.Select(tab, label.Key!, label.Lane);

        Assert.Equal("api", ColumnsNavigation.CurrentLaneKey(moved));
        Assert.Equal("X-1", moved.SelectedKey);
    }

    private static string[] Keys(IReadOnlyList<BoardColumnGroup> columns)
    {
        return [.. columns.SelectMany(column => column.Rows).Select(row => row.Key)];
    }

    private static TabState Grouped()
    {
        var definition = new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board");
        var opened = TabState.Opened(new TabId(1), definition, 1, 1, 37);
        IssueSummary[] rows =
        [
            Issue("X-1", "1", "ui", "api"),
            Issue("X-2", "1", "ui"),
            Issue("X-3", "4", "api"),
            Issue("X-4", "1"),
        ];

        return ColumnsNavigation.Resync(opened with
        {
            Load = LoadState.Idle,
            Rows = rows,
            Board = _configuration,
            SelectedKey = "X-1",
            KeyWidth = 3,
            ViewWidth = Width,
            Swimlane = SwimlaneKind.Labels,
            Lane = "ui",
            StackLanes = true,
        });
    }

    private static IssueSummary Issue(string key, string statusId, params string[] labels)
    {
        return new IssueSummary(
            key,
            key,
            "Summary",
            new IssueStatus(statusId, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            labels,
            null,
            null,
            "A");
    }
}
