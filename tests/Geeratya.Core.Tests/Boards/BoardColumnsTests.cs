using Geeratya.Core.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class BoardColumnsTests
{
    [Fact]
    public void GivenTeamManagedOrder_WhenGrouping_ThenColumnsKeepBoardOrderWithBacklogThird()
    {
        var configuration = Configuration(
            BoardType.Simple,
            Column("To Do", "10004"),
            Column("In Progress", "3"),
            Column("Backlog", "10003"),
            Column("Done", "10005"));

        var groups = BoardColumns.Group(configuration, [Row("A-1", "10003"), Row("A-2", "10004"), Row("A-3", "3")]);

        Assert.Equal(["To Do", "In Progress", "Backlog", "Done"], groups.Columns.Select(column => column.Name));
        Assert.Equal(["A-2"], groups.Columns[0].Rows.Select(row => row.Key));
        Assert.Equal(["A-3"], groups.Columns[1].Rows.Select(row => row.Key));
        Assert.Equal(["A-1"], groups.Columns[2].Rows.Select(row => row.Key));
        Assert.Empty(groups.Columns[3].Rows);
        Assert.Empty(groups.Unmapped);
    }

    [Fact]
    public void GivenCompanyManagedOrder_WhenGrouping_ThenBacklogComesFirstAndCountsFollowTheRows()
    {
        var configuration = Configuration(
            BoardType.Kanban,
            Column("Backlog", "10003"),
            Column("To Do", "10004"),
            Column("Done", "10005"));

        var groups = BoardColumns.Group(
            configuration,
            [Row("A-1", "10003"), Row("A-2", "10003"), Row("A-3", "10005")]);

        Assert.Equal(["Backlog", "To Do", "Done"], groups.Columns.Select(column => column.Name));
        Assert.Equal([2, 0, 1], groups.Columns.Select(column => column.Rows.Count));
    }

    [Fact]
    public void GivenCompanyManagedKanbanWithBacklogFirst_WhenShowingColumns_ThenBacklogColumnAndItsIssuesAreHidden()
    {
        var configuration = Configuration(
            BoardType.Kanban,
            Column("Backlog", "10003"),
            Column("To Do", "10004"),
            Column("Done", "10005"));

        var columns = BoardColumns.Visible(
            configuration,
            [Row("A-1", "10003"), Row("A-2", "10004"), Row("A-3", "10005")]);

        Assert.Equal(["To Do", "Done"], columns.Select(column => column.Name));
        Assert.Equal(["A-2"], columns[0].Rows.Select(row => row.Key));
    }

    [Fact]
    public void GivenTeamManagedBoardWithBacklogColumn_WhenShowingColumns_ThenBacklogColumnStays()
    {
        var configuration = Configuration(
            BoardType.Simple,
            Column("To Do", "10004"),
            Column("Backlog", "10003"),
            Column("Done", "10005"));

        var columns = BoardColumns.Visible(configuration, [Row("A-1", "10003")]);

        Assert.Equal(["To Do", "Backlog", "Done"], columns.Select(column => column.Name));
        Assert.Equal(["A-1"], columns[1].Rows.Select(row => row.Key));
    }

    [Fact]
    public void GivenKanbanWithoutBacklogFirst_WhenShowingColumns_ThenEveryColumnStays()
    {
        var configuration = Configuration(BoardType.Kanban, Column("To Do", "10004"), Column("Backlog", "10003"));

        Assert.Equal(2, BoardColumns.Visible(configuration, []).Count);
    }

    [Fact]
    public void GivenStatusInNoColumn_WhenGrouping_ThenTheIssueIsUnmappedAndNotLost()
    {
        var configuration = Configuration(BoardType.Kanban, Column("To Do", "10004"));

        var groups = BoardColumns.Group(configuration, [Row("A-1", "10004"), Row("A-2", "999")]);

        Assert.Equal(["A-1"], groups.Columns[0].Rows.Select(row => row.Key));
        Assert.Equal(["A-2"], groups.Unmapped.Select(row => row.Key));
    }

    [Fact]
    public void GivenColumnsWithSeveralStatuses_WhenGrouping_ThenRowOrderInsideAColumnIsKept()
    {
        var configuration = Configuration(BoardType.Simple, Column("Open", "1", "2"), Column("Closed", "3"));

        var groups = BoardColumns.Group(configuration, [Row("A-5", "2"), Row("A-1", "1"), Row("A-3", "2")]);

        Assert.Equal(["A-5", "A-1", "A-3"], groups.Columns[0].Rows.Select(row => row.Key));
    }

    [Fact]
    public void GivenStatusListedInTwoColumns_WhenGrouping_ThenTheFirstColumnWins()
    {
        var configuration = Configuration(BoardType.Kanban, Column("First", "1"), Column("Second", "1"));

        var groups = BoardColumns.Group(configuration, [Row("A-1", "1")]);

        Assert.Single(groups.Columns[0].Rows);
        Assert.Empty(groups.Columns[1].Rows);
    }

    [Fact]
    public void GivenNoColumnsAndNoRows_WhenGrouping_ThenTheResultIsEmpty()
    {
        var groups = BoardColumns.Group(Configuration(BoardType.Unknown), []);

        Assert.Empty(groups.Columns);
        Assert.Empty(groups.Unmapped);
    }

    [Fact]
    public void GivenSubtasksOfAParentInTheSameColumn_WhenArranging_ThenTheyFollowTheParentAndAreNested()
    {
        var rows = new[]
        {
            Row("A-1", "1"),
            Row("A-2", "1") with { Parent = new IssueParent("A-3", "Parent") },
            Row("A-3", "1"),
            Row("A-4", "1") with { Parent = new IssueParent("A-3", "Parent") },
            Row("A-5", "1"),
        };

        var group = BoardColumns.Arrange("Col", rows);

        Assert.Equal(["A-1", "A-3", "A-2", "A-4", "A-5"], group.Rows.Select(row => row.Key));
        Assert.Equal([false, false, true, true, false], Enumerable.Range(0, 5).Select(group.IsNested));
    }

    [Fact]
    public void GivenAParentInAnotherColumn_WhenGrouping_ThenTheChildStaysInPlaceAndIsNotNested()
    {
        var configuration = Configuration(BoardType.Kanban, Column("To Do", "1"), Column("Done", "2"));
        var child = Row("A-2", "1") with { Parent = new IssueParent("A-1", "Parent") };

        var groups = BoardColumns.Group(configuration, [Row("A-1", "2"), child, Row("A-3", "1")]);

        Assert.Equal(["A-2", "A-3"], groups.Columns[0].Rows.Select(row => row.Key));
        Assert.False(groups.Columns[0].IsNested(0));
    }

    [Fact]
    public void GivenAParentLoopOrGrandchildren_WhenArranging_ThenNoRowIsLost()
    {
        var rows = new[]
        {
            Row("A-1", "1") with { Parent = new IssueParent("A-2", null) },
            Row("A-2", "1") with { Parent = new IssueParent("A-1", null) },
            Row("A-3", "1") with { Parent = new IssueParent("A-4", null) },
            Row("A-4", "1") with { Parent = new IssueParent("A-5", null) },
            Row("A-5", "1"),
        };

        var group = BoardColumns.Arrange("Col", rows);

        Assert.Equal(["A-5", "A-4", "A-3", "A-1", "A-2"], group.Rows.Select(row => row.Key));
        Assert.Equal([false, true, true, false, true], Enumerable.Range(0, 5).Select(group.IsNested));
    }

    private static BoardConfiguration Configuration(BoardType type, params BoardColumn[] columns)
    {
        return new BoardConfiguration(1, "Board", type, columns, "10001", null, null, null);
    }

    private static BoardColumn Column(string name, params string[] statusIds)
    {
        return new BoardColumn(name, statusIds);
    }

    private static IssueSummary Row(string key, string statusId)
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
