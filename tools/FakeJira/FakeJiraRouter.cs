using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Geeratya.FakeJira;

/// <summary>Maps a request (method, path, query, body) to a reply built from recorded fixtures or generated data.</summary>
public sealed partial class FakeJiraRouter
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
    private const string JqlName = "jql";
    private const string GeneratedIssuePrefix = "/rest/api/3/issue/GEN-";
    private const string IssuePrefix = "/rest/api/3/issue/";
    private const string Get = "GET";
    private const string Post = "POST";
    private const string Put = "PUT";
    private const string Delete = "DELETE";
    private const string RankPath = "/rest/agile/1.0/issue/rank";
    private const string CreatePath = "/rest/api/3/issue";
    private const string CommentPart = "comment";
    private const string TransitionsPart = "transitions";
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
        ["/rest/api/3/issue/GCM-8"] = "issues/issue-gcm8.json",
        ["/rest/api/3/issue/GCM-10"] = "issues/issue-gcm10.json",
        ["/rest/api/3/issue/GCM-24"] = "issues/issue-gcm24.json",
        ["/rest/api/3/issue/GTM-5/comment"] = "adf/comments-gtm5.json",
        ["/rest/api/3/issue/GCM-6/comment"] = "comments/comments-gcm6.json",
        ["/rest/api/3/issue/GCM-2/comment"] = "comments/comments-empty.json",
        ["/rest/api/3/issue/GTM-5/remotelink"] = "links/remotelink-list-gtm5.json",
        ["/rest/api/3/issue/GCM-6/remotelink"] = "links/remotelink-list-gcm6.json",
        ["/rest/api/3/issue/GCM-2/remotelink"] = "links/remotelink-list-empty.json",
        ["/rest/api/3/issue/GTM-9/remotelink"] = "links/remotelink-list-empty.json",
        ["/rest/api/3/issue/GTM-11/remotelink"] = "links/remotelink-list-gtm5.json",
        ["/rest/api/3/issue/GCM-8/remotelink"] = "links/remotelink-list-empty.json",
        ["/rest/api/3/issue/GCM-10/remotelink"] = "links/remotelink-list-gcm6.json",
        ["/rest/api/3/issue/GTM-9/comment"] = "comments/comments-empty.json",
        ["/rest/api/3/issue/GTM-11/comment"] = "adf/comments-gtm5.json",
        ["/rest/api/3/issue/GCM-8/comment"] = "comments/comments-empty.json",
        ["/rest/api/3/issue/GCM-10/comment"] = "comments/comments-gcm6.json",
    };

    /// <summary>Children of an epic (<c>parent = KEY</c>) by key: one team-managed, one company-managed.</summary>
    private static readonly Dictionary<string, string> _childrenSearches = new()
    {
        ["GTM-9"] = "hierarchy/search-epic-children-gtm.json",
        ["GCM-8"] = "hierarchy/search-epic-children-gcm.json",
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
    private readonly FakeJiraWriteStore _writes;

    public FakeJiraRouter(FakeJiraOptions options)
    {
        _options = options;
        _writes = new FakeJiraWriteStore(ReadFixture, IsRecordedIssue);
    }

    /// <summary>True for the verbs that change data; the server may drop the connection after reading one.</summary>
    public static bool IsWrite(string method) => method is Post or Put or Delete;

    public FakeReply Route(string method, string path, IReadOnlyDictionary<string, string> query, string body)
    {
        if (path == SearchPath && (method == Get || method == Post))
        {
            return Search(method, query, body);
        }

        if (path == CountPath && _options.GeneratedIssues > 0)
        {
            return new FakeReply(200, GeneratedIssues.Count(_options.GeneratedIssues));
        }

        if (TryWrite(method, path, body) is { } written)
        {
            return written;
        }

        if (method == Get && TryStoredRead(path) is { } stored)
        {
            return stored;
        }

        if (method == Get && path == BoardIssuesPath)
        {
            return BoardIssues(query);
        }

        if (method == Get && _options.GeneratedIssues > 0 && TryGeneratedIssue(path, query) is { } generated)
        {
            return generated;
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

    /// <summary>
    /// The detail, comments and remote links of a generated issue (<c>GEN-n</c>, n within the dataset); null for any
    /// other path.
    /// </summary>
    private FakeReply? TryGeneratedIssue(string path, IReadOnlyDictionary<string, string> query)
    {
        if (!path.StartsWith(GeneratedIssuePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = path[GeneratedIssuePrefix.Length..].Split('/');

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || number < 1
            || number > _options.GeneratedIssues)
        {
            return null;
        }

        return parts.Length switch
        {
            1 => new FakeReply(200, GeneratedIssues.Detail(number)),
            2 when parts[1] == "comment" => new FakeReply(
                200,
                GeneratedIssues.Comments(number, IntOf(query, StartAtName, 0), IntOf(query, MaxResultsName, DefaultPageSize))),
            2 when parts[1] == "remotelink" => new FakeReply(200, GeneratedIssues.RemoteLinks(number)),
            _ => null,
        };
    }

    /// <summary>Create, edit, delete, transition, comment and rank calls; null for any other request.</summary>
    private FakeReply? TryWrite(string method, string path, string body)
    {
        if (method == Put && path == RankPath)
        {
            return _writes.Rank(body);
        }

        if (method == Post && path == CreatePath)
        {
            return _writes.Create(body);
        }

        if (!IsWrite(method) || !path.StartsWith(IssuePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = path[IssuePrefix.Length..].Split('/');
        var key = parts[0];

        return (method, parts.Length) switch
        {
            (Put, 1) => _writes.Edit(key, body),
            (Delete, 1) => _writes.Delete(key),
            (Post, 2) when parts[1] == TransitionsPart => _writes.Transition(key, body),
            (Post, 2) when parts[1] == CommentPart => _writes.AddComment(key, body),
            (Put, 3) when parts[1] == CommentPart => _writes.EditComment(key, parts[2], body),
            (Delete, 3) when parts[1] == CommentPart => _writes.DeleteComment(key, parts[2]),
            _ => null,
        };
    }

    /// <summary>
    /// The reads the write store changes: an issue (created, edited or transitioned), its comments and its
    /// transitions. Null when the store holds nothing for the path, so the recorded route answers.
    /// </summary>
    private FakeReply? TryStoredRead(string path)
    {
        if (!path.StartsWith(IssuePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = path[IssuePrefix.Length..].Split('/');
        var key = parts[0];
        var recorded = _routes.TryGetValue(path, out var fixture) ? ReadFixture(fixture) : null;

        if (parts.Length == 1)
        {
            return _writes.Issue(key, recorded) is { } issue ? new FakeReply(200, issue) : null;
        }

        if (parts.Length != 2)
        {
            return null;
        }

        if (parts[1] == TransitionsPart && (IsRecordedIssue(key) || _writes.Knows(key)))
        {
            return _writes.Transitions(key);
        }

        return parts[1] == CommentPart && _writes.Comments(key, recorded) is { } comments
            ? new FakeReply(200, comments)
            : null;
    }

    private static bool IsRecordedIssue(string key) => _routes.ContainsKey($"{IssuePrefix}{key}");

    private static int IntOf(IReadOnlyDictionary<string, string> query, string name, int fallback)
    {
        return query.TryGetValue(name, out var text) && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
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

        var jql = parameters.GetValueOrDefault(JqlName);

        if (ChildrenFixtureOf(jql) is { } children)
        {
            return new FakeReply(200, ReadFixture(children));
        }

        if (RecordedIssueOf(jql) is { } recorded)
        {
            return recorded;
        }

        if (_options.GeneratedIssues > 0)
        {
            return GeneratedPage(parameters, token);
        }

        var page = token is null ? 0 : FixturePageOf(token);

        return new FakeReply(200, ReadFixture(_searchPages[page]));
    }

    /// <summary>
    /// The board's issues by offset: the recorded page that starts at <c>startAt</c>, the last one past the end.
    /// </summary>
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

    /// <summary>The fixture for a <c>parent = KEY</c> search of a known epic; null for any other JQL.</summary>
    private static string? ChildrenFixtureOf(string? jql)
    {
        if (jql is null || ParentJql().Match(jql) is not { Success: true } match)
        {
            return null;
        }

        return _childrenSearches.GetValueOrDefault(match.Groups[1].Value);
    }

    /// <summary>
    /// A search for one recorded issue (<c>key = KEY</c>) answers a single-issue page built from its detail fixture,
    /// so a view can open issues that have relations; null for any other JQL or an unrouted key.
    /// </summary>
    private FakeReply? RecordedIssueOf(string? jql)
    {
        if (jql is null || KeyJql().Match(jql) is not { Success: true } match)
        {
            return null;
        }

        var key = match.Groups[1].Value;
        var recorded = _routes.TryGetValue(IssuePrefix + key, out var fixture) ? ReadFixture(fixture) : null;

        if ((_writes.Issue(key, recorded) ?? recorded) is not { } bytes)
        {
            return null;
        }

        var issue = Encoding.UTF8.GetString(bytes);

        return new FakeReply(200, Encoding.UTF8.GetBytes($"{{\"isLast\":true,\"issues\":[{issue}]}}"));
    }

    [GeneratedRegex(@"^\s*(?:issue)?key\s*=\s*""?([A-Za-z][A-Za-z0-9_]*-\d+)""?\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex KeyJql();

    [GeneratedRegex(
        @"^\s*parent\s*=\s*""?([A-Za-z][A-Za-z0-9_]*-\d+)""?\s*(?:order\s+by.*)?$",
        RegexOptions.IgnoreCase)]
    private static partial Regex ParentJql();

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
