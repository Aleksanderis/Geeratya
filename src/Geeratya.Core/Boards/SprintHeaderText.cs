using System.Globalization;
using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// The parts of the sprint header above a Sprint view's columns: dates, days left, issue count and points done and
/// total. Pure, so the renderer only fits the parts to the width.
/// </summary>
public static class SprintHeaderText
{

    /// <summary>The start and end date as <c>3 Nov - 17 Nov</c>; empty when the sprint has no dates.</summary>
    public static string Dates(Sprint sprint)
    {
        return ValueText.DateRange(sprint.StartDate, sprint.EndDate);
    }

    /// <summary>
    /// Time left of an active sprint, by UTC calendar day (<c>5d left</c>, <c>ends today</c>, <c>ended 2d ago</c>), or
    /// the completion date of a closed one (<c>closed 2 Nov</c>); empty for any other sprint or one without the date.
    /// </summary>
    public static string DaysLeft(Sprint sprint, DateTimeOffset now)
    {
        if (sprint is { State: SprintState.Closed, CompleteDate: { } completed })
        {
            return $"closed {Day(completed)}";
        }

        if (sprint.State != SprintState.Active || sprint.EndDate is not { } end)
        {
            return string.Empty;
        }

        var days = (end.UtcDateTime.Date - now.UtcDateTime.Date).Days;

        return days switch
        {
            0 => "ends today",
            > 0 => $"{days.ToString(CultureInfo.InvariantCulture)}d left",
            _ => $"ended {(-days).ToString(CultureInfo.InvariantCulture)}d ago",
        };
    }

    /// <summary>
    /// Points done and total as <c>8/21 pts</c>: done are the issues whose status is in the done category. Empty when
    /// no loaded issue has an estimate.
    /// </summary>
    public static string Points(IReadOnlyList<IssueSummary> rows)
    {
        double total = 0;
        double done = 0;
        var any = false;

        foreach (var row in rows)
        {
            if (row.Estimate is not { } points)
            {
                continue;
            }

            any = true;
            total += points;

            if (row.Status.CategoryKey == StatusCategoryKeys.Done)
            {
                done += points;
            }
        }

        return any ? $"{ValueText.Estimate(done)}/{ValueText.Estimate(total)} pts" : string.Empty;
    }

    /// <summary>An estimate as shown on a card: <c>5</c>, <c>0.5</c>; empty for none.</summary>
    public static string Estimate(double? points)
    {
        return ValueText.Estimate(points);
    }

    private static string Day(DateTimeOffset date)
    {
        return ValueText.Day(date);
    }
}
