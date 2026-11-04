namespace Geeratya.Core.Boards;

/// <summary>
/// The cutoff Jira's own Kanban board applies to its last (Done) column: Done issues whose status category changed
/// longer ago than N days drop off the board (Jira's "Hide done work items after"). The board load reads N from Jira
/// (the gateway, see <c>docs/jira-layer.md</c>), falling back to the <c>board.done-cutoff</c> setting, and adds it as
/// JQL because the agile API ignores it. A tab's quick choice (<c>view.done-cutoff</c>) overrides both.
/// </summary>
public static class DoneCutoff
{
    /// <summary>The default number of days (the setting <c>board.done-cutoff</c>); 0 means no cutoff.</summary>
    public const int DefaultDays = 14;

    /// <summary>The quick choice of "no cutoff": every Done issue shows.</summary>
    public const int AllDays = 0;

    private const int DaysPerWeek = 7;
    private const int MaxChoiceWeeks = 4;

    /// <summary>The choices Jira offers for the board view, in weeks (1 to 4), then all.</summary>
    public static IReadOnlyList<int> ChoiceDays { get; } = [.. Enumerable.Range(1, MaxChoiceWeeks).Select(ToDays)];

    /// <summary>
    /// The JQL clause that keeps issues not in the Done category and those whose status category changed within
    /// <paramref name="days"/> days (<c>updated</c> stands in when that date is empty), as Jira's own board counts it.
    /// </summary>
    public static string Jql(int days)
    {
        return $"statusCategory != Done OR statusCategoryChangedDate >= -{days}d "
            + $"OR (statusCategoryChangedDate is EMPTY AND updated >= -{days}d)";
    }

    /// <summary>The column title with the cutoff noted, so the user sees older issues are hidden.</summary>
    public static string Title(string columnName, int days)
    {
        return $"{columnName} ({days}d)";
    }

    /// <summary>The words of a choice: <c>1 week</c>, <c>2 weeks</c> ... <c>all</c> for none; days otherwise.</summary>
    public static string Label(int days)
    {
        if (days == AllDays)
        {
            return "all";
        }

        if (days % DaysPerWeek != 0)
        {
            return days == 1 ? "1 day" : $"{days} days";
        }

        var weeks = days / DaysPerWeek;

        return weeks == 1 ? "1 week" : $"{weeks} weeks";
    }

    /// <summary>The short word the command takes for a choice: <c>1w</c> to <c>4w</c> or <c>all</c>.</summary>
    public static string Word(int days)
    {
        return days == AllDays ? "all" : $"{days / DaysPerWeek}w";
    }

    /// <summary>
    /// Reads a command argument: <c>auto</c> (or <c>jira</c>) clears the choice, <c>all</c> shows every Done issue,
    /// <c>1w</c> to <c>4w</c> pick the weeks. Returns false for anything else.
    /// </summary>
    public static bool TryParseChoice(string argument, out int? days)
    {
        days = null;

        if (argument is "auto" or "jira")
        {
            return true;
        }

        if (argument == "all")
        {
            days = AllDays;

            return true;
        }

        foreach (var choice in ChoiceDays)
        {
            if (argument == Word(choice))
            {
                days = choice;

                return true;
            }
        }

        return false;
    }

    private static int ToDays(int weeks)
    {
        return weeks * DaysPerWeek;
    }
}
