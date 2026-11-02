using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira;

/// <summary>Maps a request (method, path, query, body) to a reply built from recorded fixtures or generated data.</summary>
public sealed class FakeJiraRouter
{
    private const string SearchPath = "/rest/api/3/search/jql";
    private const string BoardIssuesPath = "/rest/agile/1.0/board/4/issue";
    private const string StartAtName = "startAt";
    private const int BoardPageSize = 10;
    private const string CountPath = "/rest/api/3/search/approximate-count";
    private const string NotFoundFixture = "errors/issue-404.json";
    private const string FilterPrefix = "/rest/api/3/filter/";
    private const string MissingFilterFixture = "filters/filter-get-missing.json";
    private const string MaxResultsName = "maxResults";
    private const string PageTokenName = "nextPageToken";
    private const string Get = "GET";
    private const string Post = "POST";
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 5000;

    private static readonly Dictionary<string, string> _routes = new()
    {
        ["/rest/api/3/myself"] = "myself/myself.json",
        ["/rest/api/3/project/search"] = "projects/project-search.json",
        ["/rest/api/3/field"] = "metadata/field-list.json",
        ["/rest/api/3/issuetype"] = "metadata/issuetype-list.json",
        ["/rest/api/3/filter/my"] = "filters/filter-my.json",
        ["/rest/api/3/filter/search"] = "filters/filter-search.json",
        ["/rest/api/3/filter/favourite"] = "filters/filter-favourite-empty.json",
        ["/rest/agile/1.0/board"] = "boards/board-list.json",
        ["/rest/agile/1.0/board/3"] = "boards/board-gtm.json",
        ["/rest/agile/1.0/board/4"] = "boards/board-gcm.json",
        ["/rest/agile/1.0/board/3/configuration"] = "boards/board-gtm-config.json",
        ["/rest/agile/1.0/board/4/configuration"] = "boards/board-gcm-config.json",
        ["/rest/agile/1.0/board/4/backlog"] = "boards/board-gcm-backlog.json",
        ["/rest/agile/1.0/board/3/issue"] = "hierarchy/agile-board-gtm-issues.json",
        ["/rest/agile/1.0/board/3/backlog"] = "hierarchy/agile-board-gtm-backlog.json",
        ["/rest/agile/1.0/board/3/epic"] = "hierarchy/agile-board-epics-gtm.json",
        ["/rest/agile/1.0/board/4/epic"] = "hierarchy/agile-board-epics-gcm.json",
        ["/rest/api/3/filter/10001"] = "filters/filter-get.json",
        ["/rest/api/3/issue/GTM-9"] = "issues/issue-gtm9.json",
        ["/rest/api/3/issue/GTM-11"] = "issues/issue-gtm11.json",
        ["/rest/api/3/issue/GTM-20"] = "issues/issue-gtm20.json",
        ["/rest/api/3/issue/GCM-2"] = "issues/issue-gcm2.json",
        ["/rest/api/3/issue/GCM-6"] = "issues/issue-gcm6.json",
        ["/rest/api/3/issue/GCM-10"] = "issues/issue-gcm10.json",
        ["/rest/api/3/issue/GCM-24"] = "issues/issue-gcm24.json",
        ["/rest/api/3/issue/GTM-5/comment"] = "adf/comments-gtm5.json",
        ["/rest/api/3/issue/GCM-6/comment"] = "comments/comments-gcm6.json",
        ["/rest/api/3/issue/GCM-2/comment"] = "comments/comments-empty.json",
        ["/rest/api/3/issue/GTM-5/remotelink"] = "links/remotelink-list-gtm5.json",
        ["/rest/api/3/issue/GCM-6/remotelink"] = "links/remotelink-list-gcm6.json",
        ["/rest/api/3/issue/GCM-2/remotelink"] = "links/remotelink-list-empty.json",
    };

    /// <summary>The recorded pages of the company-managed board's issues, 10 per page, by <c>startAt</c>.</summary>
    private static readonly string[] _boardIssuePages =
    [
        "boards/rows-gcm-issues-page1.json",
        "boards/rows-gcm-issues-page2.json",
        "boards/rows-gcm-issues-page3-last.json",
    ];

    private static readonly string[] _searchPages =
    [
        "search/search-page1.json",
        "search/search-page2.json",
        "search/search-page3-last.json",
    ];

    private readonly FakeJiraOptions _options;
    private readonly Dictionary<string, byte[]> _fixtureCache = [];
    private readonly Lock _cacheLock = new();

    public FakeJiraRouter(FakeJiraOptions options)
    {
        _options = options;
    }

    public FakeReply Route(string method, string path, IReadOnlyDictionary<string, string> query, string body)
    {
        if (path == SearchPath && method is Get or Post)
        {
            return Search(method, query, body);
        }

        if (path == CountPath && _options.GeneratedIssues > 0)
        {
            return new FakeReply(200, GeneratedIssues.Count(_options.GeneratedIssues));
        }

        if (method == Get && path == BoardIssuesPath)
        {
            return BoardIssues(query);
        }

        if (method == Get && _routes.TryGetValue(path, out var fixture))
        {
            return new FakeReply(200, ReadFixture(fixture));
        }

        if (method == Get && path.StartsWith(FilterPrefix, StringComparison.Ordinal)
            && long.TryParse(
                path.AsSpan(FilterPrefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _))
        {
            return new FakeReply(404, ReadFixture(MissingFilterFixture));
        }

        return new FakeReply(404, ReadFixture(NotFoundFixture));
    }

    private static Dictionary<string, string> ReadBody(string body)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(body))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(body);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ToString();
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return result;
        }

        return result;
    }

    private FakeReply Search(string method, IReadOnlyDictionary<string, string> query, string body)
    {
        var parameters = method == Post ? ReadBody(body) : query;
        var token = parameters.GetValueOrDefault(PageTokenName);

        if (_options.GeneratedIssues > 0)
        {
            return GeneratedPage(parameters, token);
        }

        var page = token is null ? 0 : FixturePageOf(token);

        return new FakeReply(200, ReadFixture(_searchPages[page]));
    }

    /// <summary>The board's issues by offset: the recorded page that starts at <c>startAt</c>, the last one past the end.</summary>
    private FakeReply BoardIssues(IReadOnlyDictionary<string, string> query)
    {
        var start = 0;

        if (query.TryGetValue(StartAtName, out var text) && (!int.TryParse(text, out start) || start < 0))
        {
            start = 0;
        }

        var page = Math.Min(start / BoardPageSize, _boardIssuePages.Length - 1);

        return new FakeReply(200, ReadFixture(_boardIssuePages[page]));
    }

    private FakeReply GeneratedPage(IReadOnlyDictionary<string, string> parameters, string? token)
    {
        var pageSize = DefaultPageSize;

        if (parameters.TryGetValue(MaxResultsName, out var text) && int.TryParse(text, out var requested)
            && requested > 0)
        {
            pageSize = Math.Min(requested, MaxPageSize);
        }

        var start = 0;

        if (token is not null && (!int.TryParse(token, out start) || start < 0))
        {
            return new FakeReply(400, Encoding.UTF8.GetBytes("{\"errorMessages\":[\"Invalid page token.\"]}"));
        }

        return new FakeReply(200, GeneratedIssues.Page(_options.GeneratedIssues, start, pageSize));
    }

    private int FixturePageOf(string token)
    {
        for (var index = 0; index < _searchPages.Length - 1; index++)
        {
            var text = Encoding.UTF8.GetString(ReadFixture(_searchPages[index]));

            if (text.Contains($"\"{PageTokenName}\": \"{token}\"", StringComparison.Ordinal))
            {
                return index + 1;
            }
        }

        return _searchPages.Length - 1;
    }

    private byte[] ReadFixture(string relativePath)
    {
        lock (_cacheLock)
        {
            if (!_fixtureCache.TryGetValue(relativePath, out var bytes))
            {
                bytes = File.ReadAllBytes(Path.Combine(_options.FixturesDirectory, relativePath));
                _fixtureCache[relativePath] = bytes;
            }

            return bytes;
        }
    }
}
