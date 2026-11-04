namespace Geeratya.Core.Boards;

/// <summary>
/// The cutoff Jira's own Kanban board applies to its last (Done) column: issues resolved longer ago than
/// the <c>board.done-cutoff</c> setting (days) drop off the board. The agile API does not expose it (Jira's
/// "Hide done work items after"), so the board load adds it as JQL.
/// </summary>
public static class DoneCutoff
{
    /// <summary>The default number of days (the setting <c>board.done-cutoff</c>); 0 means no cutoff.</summary>
    public const int DefaultDays = 14;

    /// <summary>The JQL clause that keeps open issues and those resolved within <paramref name="days"/> days.</summary>
    public static string Jql(int days)
    {
        return $"resolution = Unresolved OR resolutiondate >= -{days}d";
    }

    /// <summary>The column title with the cutoff noted, so the user sees older issues are hidden.</summary>
    public static string Title(string columnName, int days)
    {
        return $"{columnName} ({days}d)";
    }
}
