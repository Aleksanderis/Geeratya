using System.Text.Json;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Search;

/// <summary>
/// JQL search over <c>/rest/api/3/search/jql</c> with token paging. Reads only. GET while the URL is short, POST
/// (same parameters as a JSON body) when the JQL is long. The caller owns <c>ORDER BY</c> and the page size.
/// </summary>
public sealed class JiraSearchClient(JiraTransport transport)
{
    public const string SearchPath = "/rest/api/3/search/jql";
    public const string ApproximateCountPath = "/rest/api/3/search/approximate-count";

    /// <summary>The largest <c>maxResults</c> Jira accepts (400 above it); probed on the dev site.</summary>
    public const int MaxPageSize = 5000;

    /// <summary>A request path longer than this is sent as POST so proxies and Jira do not reject the URL.</summary>
    public const int MaxGetPathLength = 2000;

    private const string PageExpiredMessage = "The page expired, reload the results.";

    /// <summary>Loads one page. Pass the previous page's <c>Next</c> to continue, null for the first page.</summary>
    public async Task<JiraResult<IssuePage>> SearchAsync(
        IssueSearchQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var request = BuildRequest(query, pageSize, cursor?.Token) with { Priority = query.Priority };

        var result = await transport.SendAsync(request, SearchJsonContext.Default.Response, cancellationToken);

        if (!result.IsSuccess)
        {
            return JiraResults.Failure<IssuePage>(ExpireCursor(result.Error, cursor));
        }

        return JiraResults.Success(IssueSummaryMapper.MapPage(result.Value, query.Extras));
    }

    /// <summary>The approximate number of matches, good for an "about N" label only.</summary>
    public async Task<JiraResult<int>> ApproximateCountAsync(
        IssueSearchQuery query,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new SearchWire.CountBody(query.Jql), SearchJsonContext.Default.CountBody);
        var request = JiraRequest.PostSearch(ApproximateCountPath, body) with { Priority = query.Priority };

        var result = await transport.SendAsync(request, SearchJsonContext.Default.CountResponse, cancellationToken);

        return result.IsSuccess ? JiraResults.Success(result.Value.Count) : JiraResults.Failure<int>(result.Error);
    }

    private static JiraRequest BuildRequest(IssueSearchQuery query, int pageSize, string? token)
    {
        var path = BuildGetPath(query, pageSize, token);

        if (path.Length <= MaxGetPathLength)
        {
            return JiraRequest.Get(path);
        }

        var fields = query.Extras.FieldList(IssueFields.ListRow);
        var body = new SearchWire.Body(query.Jql, pageSize, fields, token);

        return JiraRequest.PostSearch(SearchPath, JsonSerializer.Serialize(body, SearchJsonContext.Default.Body));
    }

    private static string BuildGetPath(IssueSearchQuery query, int pageSize, string? token)
    {
        var fields = query.Extras.QueryValue(IssueFields.ListRow);
        var parameters = $"jql={Uri.EscapeDataString(query.Jql)}&fields={fields}&maxResults={pageSize}";
        var path = $"{SearchPath}?{parameters}";

        return token is null ? path : $"{path}&nextPageToken={Uri.EscapeDataString(token)}";
    }

    /// <summary>A 400 on a continued search means the token is bad or expired, not that the JQL is wrong.</summary>
    private static JiraError ExpireCursor(JiraError error, PageCursor? cursor)
    {
        if (cursor is null || error.Kind != JiraErrorKind.BadRequest)
        {
            return error;
        }

        return error with { Kind = JiraErrorKind.PageExpired, Message = $"{PageExpiredMessage} {error.Message}" };
    }
}
