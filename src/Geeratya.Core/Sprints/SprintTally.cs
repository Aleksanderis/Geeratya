using System.Globalization;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// The loaded issues of one sprint of a tab, counted for the Start and Complete questions: how many, their points,
/// which are open (status not in the done category). <paramref name="IsComplete"/> is false while the tab has more
/// pages or was cut, so the numbers are a lower bound. Pure.
/// </summary>
public sealed record SprintTally(
    int Count,
    double Points,
    IReadOnlyList<string> OpenKeys,
    bool IsComplete)
{
    private const string DoneCategory = "done";

    /// <summary>The issues done.</summary>
    public int DoneCount => Count - OpenKeys.Count;

    /// <summary>Counts the rows of <paramref name="sprint"/> in <paramref name="tab"/>.</summary>
    public static SprintTally Of(TabState tab, Sprint sprint)
    {
        var rows = tab.Definition.Source == ViewSourceKind.Sprint
            ? tab.Rows
            : [.. tab.Rows.Where(row => row.CurrentSprint?.Id == sprint.Id)];
        var complete = tab.Next is null && !tab.Truncated && tab.PendingRows is null;

        return new SprintTally(
            rows.Count,
            rows.Sum(row => row.Estimate ?? 0),
            [.. rows.Where(IsOpen).Select(row => row.Key)],
            complete);
    }

    /// <summary>A number of points as <c>34</c> or <c>0.5</c>.</summary>
    public static string Number(double value)
    {
        return value.ToString("0.#", CultureInfo.InvariantCulture);
    }

    /// <summary><c>1 issue</c>, <c>12 issues</c>.</summary>
    public static string Issues(int count)
    {
        var number = count.ToString(CultureInfo.InvariantCulture);

        return count == 1 ? $"{number} issue" : $"{number} issues";
    }

    private static bool IsOpen(IssueSummary row)
    {
        return row.Status.CategoryKey != DoneCategory;
    }
}
