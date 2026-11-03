using System.Globalization;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Boards;

/// <summary>
/// The parts of the sprint header above a Sprint view's columns: dates, days left, issue count and points done and
/// total. Pure, so the renderer only fits the parts to the width.
/// </summary>
public static class SprintHeaderText
{
    private const string DoneCategory = "done";
    private const string DateFormat = "d MMM";

    /// <summary>The start and end date as <c>3 Nov - 17 Nov</c>; empty when the sprint has no dates.</summary>
    public static string Dates(Sprint sprint)
    {
        if (sprint.StartDate is not { } start || sprint.EndDate is not { } end)
        {
            return string.Empty;
        }

        return $"{Day(start)} - {Day(end)}";
    }

    /// <summary>
    /// Time left of an active sprint (<c>5d left</c>, <c>ends today</c>, <c>ended 2d ago</c>); empty for a sprint that
    /// is not active or has no end date.
    /// </summary>
    public static string DaysLeft(Sprint sprint, DateTimeOffset now)
    {
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

            if (row.Status.CategoryKey == DoneCategory)
            {
                done += points;
            }
        }

        return any ? $"{Number(done)}/{Number(total)} pts" : string.Empty;
    }

    /// <summary>An estimate as shown on a card: <c>5</c>, <c>0.5</c>; empty for none.</summary>
    public static string Estimate(double? points)
    {
        return points is { } value ? $"{Number(value)}" : string.Empty;
    }

    private static string Number(double value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    private static string Day(DateTimeOffset date)
    {
        return date.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture);
    }
}
