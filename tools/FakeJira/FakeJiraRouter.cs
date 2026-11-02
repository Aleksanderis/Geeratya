using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira;

/// <summary>Maps a request (method, path, query, body) to a reply built from recorded fixtures or generated data.</summary>
public sealed class FakeJiraRouter
{
    private const string SearchPath = "/rest/api/3/search/jql";
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
        ["/rest/agile/1.0/board"] = "boards/board-list.json",
        ["/rest/agile/1.0/board/3"] = "boards/board-gtm.json",
        ["/rest/agile/1.0/board/4"] = "boards/board-gcm.json",
        ["/rest/agile/1.0/board/3/configuration"] = "boards/board-gtm-config.json",
        ["/rest/agile/1.0/board/4/configuration"] = "boards/board-gcm-config.json",
        ["/rest/agile/1.0/board/4/backlog"] = "boards/board-gcm-backlog.json",
        ["/rest/agile/1.0/board/4/issue"] = "boards/board-gcm-issues.json",
        ["/rest/agile/1.0/board/3/issue"] = "hierarchy/agile-board-gtm-issues.json",
        ["/rest/agile/1.0/board/3/backlog"] = "hierarchy/agile-board-gtm-backlog.json",
        ["/rest/agile/1.0/board/3/epic"] = "hierarchy/agile-board-epics-gtm.json",
        ["/rest/agile/1.0/board/4/epic"] = "hierarchy/agile-board-epics-gcm.json",
        ["/rest/api/3/filter/10001"] = "filters/filter-get.json",
    };

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

        if (method == Get && _routes.TryGetValue(path, out var fixture))
        {
            return new FakeReply(200, ReadFixture(fixture));
        }

        if (method == Get && path.StartsWith(FilterPrefix, StringComparison.Ordinal)
            && long.TryParse(path.AsSpan(FilterPrefix.Length), out _))
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
