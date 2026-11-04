namespace Geeratya.Core.Boards;

/// <summary>
/// The cutoff Jira's own Kanban board applies to its last (Done) column: issues resolved longer ago than
/// <see cref="Days"/> days drop off the board. The agile API does not expose it, so the board load adds it as JQL.
/// </summary>
public static class DoneCutoff
{
    /// <summary>How many days back a resolved issue still shows on a Kanban board.</summary>
    public const int Days = 14;

    /// <summary>The JQL clause that keeps open issues and recently resolved ones.</summary>
    public static readonly string Jql = $"resolution = Unresolved OR resolutiondate >= -{Days}d";

    /// <summary>The column title with the cutoff noted, so the user sees older issues are hidden.</summary>
    public static string Title(string columnName)
    {
        return $"{columnName} ({Days}d)";
    }
}
