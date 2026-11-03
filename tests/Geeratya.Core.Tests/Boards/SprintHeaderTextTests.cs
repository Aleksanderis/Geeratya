using Geeratya.Core.Boards;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Tests.Boards;

[Trait("Os", "Independent")]
public class SprintHeaderTextTests
{
    private static readonly DateTimeOffset _now = new(2026, 11, 3, 23, 30, 0, TimeSpan.Zero);

    [Fact]
    public void GivenDates_WhenFormatted_ThenStartAndEndShowAndMissingDatesGiveNothing()
    {
        var dated = Sprint(SprintState.Active, new DateTimeOffset(2026, 10, 30, 9, 0, 0, TimeSpan.Zero), 13);

        Assert.Equal("30 Oct - 13 Nov", SprintHeaderText.Dates(dated));
        Assert.Equal(string.Empty, SprintHeaderText.Dates(dated with { StartDate = null }));
    }

    [Theory]
    [InlineData(10, "7d left")]
    [InlineData(3, "ends today")]
    [InlineData(1, "ended 2d ago")]
    public void GivenAnActiveSprint_WhenAskingDaysLeft_ThenItCountsCalendarDays(int endDay, string expected)
    {
        var sprint = Sprint(SprintState.Active, null, endDay);

        Assert.Equal(expected, SprintHeaderText.DaysLeft(sprint, _now));
    }

    [Fact]
    public void GivenAClosedSprintOrNoEnd_WhenAskingDaysLeft_ThenNothingShows()
    {
        Assert.Equal(string.Empty, SprintHeaderText.DaysLeft(Sprint(SprintState.Closed, null, 10), _now));
        Assert.Equal(
            string.Empty,
            SprintHeaderText.DaysLeft(Sprint(SprintState.Active, null, 10) with { EndDate = null }, _now));
    }

    [Fact]
    public void GivenEstimates_WhenSummed_ThenDoneCategoryCountsAsDoneAndFractionsKeepOneDecimal()
    {
        var rows = new[] { Row("new", 5), Row("done", 3), Row("done", 0.5), Row("indeterminate", null) };

        Assert.Equal("3.5/8.5 pts", SprintHeaderText.Points(rows));
    }

    [Fact]
    public void GivenNoEstimates_WhenSummed_ThenNothingShows()
    {
        Assert.Equal(string.Empty, SprintHeaderText.Points([Row("new", null)]));
        Assert.Equal(string.Empty, SprintHeaderText.Points([]));
    }

    private static Sprint Sprint(SprintState state, DateTimeOffset? start, int endDay)
    {
        return new Sprint(
            1,
            "S",
            state,
            null,
            start,
            new DateTimeOffset(2026, 11, endDay, 9, 0, 0, TimeSpan.Zero),
            null,
            1);
    }

    private static IssueSummary Row(string category, double? points)
    {
        return new IssueSummary(
            "1",
            "A-1",
            "S",
            new IssueStatus("1", "St", category),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A") { Estimate = points };
    }
}
