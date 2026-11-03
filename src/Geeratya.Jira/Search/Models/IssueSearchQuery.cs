using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Search.Models;

/// <summary>A JQL search: the query as typed (or built by the JQL builder), the page size and who is waiting.</summary>
/// <param name="Jql">The JQL, including an explicit <c>ORDER BY</c>; the client never adds one.</param>
/// <param name="PageSize">Issues per page, clamped to 1 to <see cref="JiraSearchClient.MaxPageSize"/>.</param>
public sealed record IssueSearchQuery(string Jql, int PageSize = IssueSearchQuery.DefaultPageSize)
{
    public const int DefaultPageSize = 50;

    /// <summary>Who is waiting for the answer. Defaults to <see cref="JiraPriority.User"/>.</summary>
    public JiraPriority Priority { get; init; } = JiraPriority.User;

    /// <summary>Site-specific fields (Sprint, estimate) to read onto the rows; none by default.</summary>
    public IssueExtraFields Extras { get; init; } = IssueExtraFields.None;

    /// <summary>The same search as background work (refresh, prefetch): yields to user requests.</summary>
    public IssueSearchQuery AsBackground() => this with { Priority = JiraPriority.Background };
}
