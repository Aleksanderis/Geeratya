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
