using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Boards.Models;

/// <summary>Which issues of a board (or its backlog) to load and how many per page.</summary>
/// <param name="PageSize">Issues per page, clamped to 1 to <see cref="BoardClient.MaxPageSize"/>.</param>
/// <param name="Jql">Extra JQL that narrows the board's own result; null for none. Board order is kept.</param>
public sealed record BoardIssueQuery(int PageSize = BoardIssueQuery.DefaultPageSize, string? Jql = null)
{
    public const int DefaultPageSize = 100;

    /// <summary>Who is waiting for the answer. Defaults to <see cref="JiraPriority.User"/>.</summary>
    public JiraPriority Priority { get; init; } = JiraPriority.User;

    /// <summary>The same query as background work (refresh): yields to user requests.</summary>
    public BoardIssueQuery AsBackground() => this with { Priority = JiraPriority.Background };
}
