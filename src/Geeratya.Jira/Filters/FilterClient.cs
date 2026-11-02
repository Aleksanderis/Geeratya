using System.Globalization;
using Geeratya.Jira.Filters.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Filters;

/// <summary>
/// Reads saved filters: search, the caller's own, favourites and one by id. Reads only; creating, sharing and
/// editing filters is out of scope. A filter is run as plain JQL (<c>filter = &lt;id&gt;</c>) through the search
/// client. A missing or hidden filter is a <see cref="JiraErrorKind.NotFound"/> ("not found or no access").
/// </summary>
public sealed class FilterClient(JiraTransport transport)
{
    private const string FilterPath = "/rest/api/3/filter";

    /// <summary>Largest page size requested from <c>filter/search</c>.</summary>
    public const int MaxPageSize = 100;

    private const string Expand = "jql,owner";

    public async Task<JiraResult<OffsetPage<SavedFilter>>> SearchAsync(
        FilterSearchQuery query,
        int startAt,
        CancellationToken cancellationToken)
    {
        var url = $"{FilterPath}/search?expand={Expand}&startAt={Number(Math.Max(startAt, 0))}" +
                  $"&maxResults={Number(Math.Clamp(query.PageSize, 1, MaxPageSize))}";

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            url += $"&filterName={Uri.EscapeDataString(query.Name)}";
        }

        var request = JiraRequest.Get(url) with { Priority = query.Priority };
        var result = await transport.SendAsync(request, FilterJsonContext.Default.FilterPage, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(FilterMapper.MapPage(result.Value))
            : JiraResults.Failure<OffsetPage<SavedFilter>>(result.Error);
    }

    /// <summary>The caller's own filters (a bare array, not paged).</summary>
    public Task<JiraResult<IReadOnlyList<SavedFilter>>> GetMyAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User) =>
        GetListAsync($"{FilterPath}/my?expand={Expand}", false, priority, cancellationToken);

    /// <summary>The caller's starred filters (a bare array, not paged; often empty).</summary>
    /// <remarks>Every entry is marked as favourite, whether or not Jira sends the flag on this endpoint.</remarks>
    public Task<JiraResult<IReadOnlyList<SavedFilter>>> GetFavouritesAsync(
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User) =>
        GetListAsync($"{FilterPath}/favourite?expand={Expand}", true, priority, cancellationToken);

    /// <summary>Loads one filter. An id that is not all digits cannot exist and is answered as NotFound locally.</summary>
    public async Task<JiraResult<SavedFilter>> GetAsync(
        string filterId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        if (filterId.Length == 0 || !filterId.All(char.IsAsciiDigit))
        {
            return JiraResults.Failure<SavedFilter>(
                new JiraError(JiraErrorKind.NotFound, null, "Not found or no access."));
        }

        var request = JiraRequest.Get($"{FilterPath}/{filterId}?expand={Expand}") with { Priority = priority };
        var result = await transport.SendAsync(request, FilterJsonContext.Default.Filter, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(FilterMapper.Map(result.Value))
            : JiraResults.Failure<SavedFilter>(result.Error);
    }

    private async Task<JiraResult<IReadOnlyList<SavedFilter>>> GetListAsync(
        string url,
        bool markFavourite,
        JiraPriority priority,
        CancellationToken cancellationToken)
    {
        var result = await transport.SendAsync(
            JiraRequest.Get(url) with { Priority = priority },
            FilterJsonContext.Default.ListFilter,
            cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(FilterMapper.MapList(result.Value, markFavourite))
            : JiraResults.Failure<IReadOnlyList<SavedFilter>>(result.Error);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
