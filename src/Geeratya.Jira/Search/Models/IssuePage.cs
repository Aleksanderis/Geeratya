using Geeratya.Jira.Paging;

namespace Geeratya.Jira.Search.Models;

/// <summary>One page of search results. A search has no total, so the count comes from a separate call.</summary>
/// <param name="Items">The issues of this page, in the order of the query's <c>ORDER BY</c>.</param>
/// <param name="Next">Continues the search; null on the last page.</param>
/// <param name="Total">The total when known; null for token-paged search.</param>
public sealed record IssuePage(IReadOnlyList<IssueSummary> Items, PageCursor? Next, int? Total)
{
    public bool IsLast => Next is null;
}
