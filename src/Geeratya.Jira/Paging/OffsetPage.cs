namespace Geeratya.Jira.Paging;

/// <summary>
/// One page of an offset-paged list (<c>startAt</c>, <c>maxResults</c>, <c>total</c>), as the agile and filter
/// endpoints return it.
/// </summary>
/// <param name="Items">The items of this page.</param>
/// <param name="StartAt">Offset of the first item.</param>
/// <param name="PageSize">The page size Jira used.</param>
/// <param name="Total">The total when Jira says it; some endpoints omit it.</param>
/// <param name="IsLast">Whether no page follows.</param>
public sealed record OffsetPage<T>(IReadOnlyList<T> Items, int StartAt, int PageSize, int? Total, bool IsLast)
{
    /// <summary>The offset of the following page, or null on the last page.</summary>
    public int? NextStartAt => IsLast ? null : StartAt + Items.Count;
}
