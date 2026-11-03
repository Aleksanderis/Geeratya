using Geeratya.Core.Lists;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Tests.Lists;

[Trait("Os", "Independent")]
public class ListColumnsTests
{
    [Theory]
    [InlineData(120, 16, 14)]
    [InlineData(100, 16, 14)]
    [InlineData(99, 0, 14)]
    [InlineData(56, 0, 14)]
    [InlineData(55, 0, 1)]
    [InlineData(40, 0, 1)]
    public void GivenAWidth_WhenChoosingColumns_ThenAssigneeThenStatusNameDrop(int width, int assignee, int status)
    {
        var columns = ListColumns.For(width, 6);

        Assert.Equal(assignee, columns.Assignee);
        Assert.Equal(status, columns.Status);
        Assert.Equal(6, columns.Key);
    }

    [Fact]
    public void GivenOptionalColumns_WhenSummaryWouldShrinkBelowTheMinimum_ThenLaterColumnsDropFirst()
    {
        var base120 = ListColumns.For(100, 6);
        var columns = ListColumns.For(100, 6, true, true, true);

        Assert.True(columns.Summary >= ListColumns.MinSummaryWidth);
        Assert.Equal(ListColumns.SprintWidth, columns.Sprint);
        Assert.Equal(ListColumns.PointsWidth, columns.Points);
        Assert.Equal(0, columns.Epic);
        Assert.Equal(base120.Summary - ListColumns.SprintWidth - ListColumns.PointsWidth - 2, columns.Summary);
    }

    [Fact]
    public void GivenATightWidth_WhenSprintDoesNotFitButPointsDoes_ThenOnlyPointsShow()
    {
        // Width 80 leaves 57 summary cells: sprint (17) fits, so shrink further to find the boundary.
        var columns = ListColumns.For(55 + ListColumns.PointsWidth + 1 + 9, 6, true, true, false);

        Assert.Equal(0, columns.Sprint);
        Assert.Equal(ListColumns.PointsWidth, columns.Points);
    }

    [Theory]
    [InlineData(120)]
    [InlineData(80)]
    [InlineData(40)]
    public void GivenTypicalWidths_WhenChoosingColumns_ThenTheRowFillsExactlyTheWidth(int width)
    {
        var columns = ListColumns.For(width, 6);
        var gaps = columns.Assignee > 0 ? 4 : 3;
        var total = ListColumns.MarkWidth + columns.Key + 1 + columns.Summary + columns.Status + columns.Assignee + gaps;

        Assert.Equal(width, total);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(7, 7)]
    [InlineData(40, 12)]
    public void GivenLoadedKeyWidth_WhenChoosingColumns_ThenKeyIsClamped(int loaded, int expected)
    {
        Assert.Equal(expected, ListColumns.For(120, loaded).Key);
    }

    [Fact]
    public void GivenTinyWidths_WhenChoosingColumns_ThenSummaryIsNeverNegative()
    {
        for (var width = 0; width <= 30; width++)
        {
            Assert.True(ListColumns.For(width, 12).Summary >= 0);
        }
    }

    [Theory]
    [InlineData("Epic", 1, false, 'E')]
    [InlineData("Initiative", 2, false, 'E')]
    [InlineData("Story", 0, false, 'S')]
    [InlineData("task", 0, false, 'T')]
    [InlineData("Bug", 0, false, 'B')]
    [InlineData("Sub-task", -1, true, 's')]
    [InlineData("Subtask", 0, true, 's')]
    [InlineData("Incident", 0, false, '?')]
    [InlineData("Aufgabe", 0, false, '?')]
    public void GivenAnIssueType_WhenMarking_ThenHierarchyAndNameDecide(string name, int level, bool subtask, char marker)
    {
        Assert.Equal(marker, IssueTypeMarker.For(new IssueTypeSummary("1", name, level, subtask)));
    }
}
