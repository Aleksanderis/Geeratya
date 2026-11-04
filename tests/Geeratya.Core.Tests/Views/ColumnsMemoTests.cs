using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Views;

[Trait("Os", "Independent")]
public class ColumnsMemoTests
{
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
    public void GivenTheSameInputs_WhenAskingTwice_ThenTheSameColumnsComeBack()
    {
        var tab = Tab([Issue("A-1", "1")]);

        Assert.Same(ColumnsNavigation.ColumnsOf(tab), ColumnsNavigation.ColumnsOf(tab with { Scroll = 3 }));
    }

    [Fact]
    public void GivenCachedColumns_WhenTheRowsChange_ThenTheColumnsAreRebuilt()
    {
        var tab = Tab([Issue("A-1", "1")]);
        Assert.Single(ColumnsNavigation.ColumnsOf(tab)[0].Rows);

        var changed = tab with { Rows = [Issue("A-1", "1"), Issue("A-2", "1")] };

        Assert.Equal(2, ColumnsNavigation.ColumnsOf(changed)[0].Rows.Count);
    }

    [Fact]
    public void GivenCachedColumns_WhenACardIsFoldedOut_ThenTheListedSubtaskRowAppears()
    {
        var parent = Issue("A-1", "1") with
        {
            Subtasks = [new IssueSubtask("A-2", "Child", new IssueStatus("1", "To Do", "new"), "Sub-task")],
        };
        var tab = Tab([parent]);
        Assert.Single(ColumnsNavigation.ColumnsOf(tab)[0].Rows);

        var expanded = tab with { ExpandedSubtasks = ["A-1"] };

        Assert.Equal(2, ColumnsNavigation.ColumnsOf(expanded)[0].Rows.Count);
    }

    [Fact]
    public void GivenCachedColumns_WhenTheCutOffChanges_ThenTheLastColumnTitleFollows()
    {
        var tab = Tab([Issue("A-1", "4")]);
        var before = ColumnsNavigation.ColumnsOf(tab)[1].Name;

        var after = ColumnsNavigation.ColumnsOf(tab with { DoneCutoffDays = 14 })[1].Name;

        Assert.NotEqual(before, after);
    }

    private static TabState Tab(IReadOnlyList<IssueSummary> rows)
    {
        return new TabState(
            new TabId(1),
            new ViewDefinition(ViewSourceKind.Board, "4", ViewLayout.Columns, "Board"),
            rows,
            null,
            false,
            0,
            null,
            0,
            20,
            LoadState.Idle,
            0,
            0,
            false,
            null,
            null,
            false,
            4,
            _configuration);
    }

    private static IssueSummary Issue(string key, string statusId)
    {
        return new IssueSummary(
            key,
            key,
            key,
            new IssueStatus(statusId, "Status", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }
}
