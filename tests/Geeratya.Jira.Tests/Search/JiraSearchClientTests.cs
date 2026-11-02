using System.Net;
using System.Text.Json;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Search;

public sealed class JiraSearchClientTests
{
    private const string SearchPath = JiraSearchClient.SearchPath;

    [Fact]
    public async Task GivenCompanyManagedFixture_WhenSearching_ThenRowsAreMappedWithParentSubtaskAndCategory()
    {
        using var fixture = Serving("search/search-rows-gcm.json");

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.True(result.IsSuccess);
        var page = result.Value;
        Assert.True(page.IsLast);
        Assert.Null(page.Total);
        Assert.Equal(24, page.Items.Count);
        var subtask = page.Items.Single(issue => issue.Key == "GCM-6");
        Assert.True(subtask.IssueType.IsSubtask);
        Assert.Equal(-1, subtask.IssueType.HierarchyLevel);
        Assert.Equal("GCM-1", subtask.Parent?.Key);
        Assert.Equal("Survey task one", subtask.Parent?.Summary);
        var epic = page.Items.Single(issue => issue.Key == "GCM-5");
        Assert.Equal(1, epic.IssueType.HierarchyLevel);
        Assert.Null(epic.Parent);
        var done = page.Items.Single(issue => issue.Key == "GCM-12");
        Assert.Equal("done", done.Status.CategoryKey);
        Assert.Equal("GCM", done.ProjectKey);
        Assert.Equal("Medium", done.Priority);
        Assert.Null(done.Assignee);
        Assert.Equal(new DateTimeOffset(2026, 11, 1, 17, 48, 14, 940, TimeSpan.FromHours(2)), done.Updated);
    }

    [Fact]
    public async Task GivenRecordedSubTasksOfReply_WhenSearching_ThenTheSubtaskIsFoundWithItsTypeFlag()
    {
        using var fixture = Serving("search/search-subtasks-of-gtm.json");

        var result = await FirstPageAsync(fixture, JqlBuilder.SubTasksOf("GTM-32"));

        Assert.True(result.IsSuccess);
        var subtask = Assert.Single(result.Value.Items);
        Assert.Equal("GTM-33", subtask.Key);
        Assert.True(subtask.IssueType.IsSubtask);
        Assert.Contains("parent%20%3D%20GTM-32", Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GivenTeamManagedFixture_WhenSearching_ThenSubtasksAreFoundByFlagAndCategoryNotByName()
    {
        using var fixture = Serving("search/search-rows-gtm.json");

        var result = await FirstPageAsync(fixture, "project = GTM");

        var page = result.Value;
        Assert.Equal(16, page.Items.Count);
        var subtasks = page.Items.Where(issue => issue.IssueType.IsSubtask).Select(issue => issue.Key).ToList();
        Assert.Equal(["GTM-19", "GTM-20"], subtasks);
        Assert.All(page.Items, issue => Assert.Equal("GTM", issue.ProjectKey));
        var toDo = page.Items.First(issue => issue.Status.Name == "To Do");
        Assert.Equal("indeterminate", toDo.Status.CategoryKey);
        var backlog = page.Items.First(issue => issue.Status.Name == "Backlog");
        Assert.Equal("new", backlog.Status.CategoryKey);
        Assert.Equal("GTM-9", page.Items.Single(issue => issue.Key == "GTM-11").Parent?.Key);
    }

    [Fact]
    public async Task GivenQuery_WhenSearching_ThenGetAsksOnlyForListRowFieldsAndLeavesOrderByToCaller()
    {
        using var fixture = Serving("search/search-empty.json");
        const string jql = "project = GCM AND summary ~ \"a&b\" ORDER BY updated DESC";

        await Client(fixture).SearchAsync(new IssueSearchQuery(jql, 25), null, CancellationToken.None);

        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        var query = System.Web.HttpUtility.ParseQueryString(new Uri($"https://x{request.PathAndQuery}").Query);
        Assert.Equal(jql, query["jql"]);
        Assert.Equal(string.Join(',', IssueFields.ListRow), query["fields"]);
        Assert.Equal("25", query["maxResults"]);
        Assert.Null(query["nextPageToken"]);
        Assert.DoesNotContain("*", request.PathAndQuery);
    }

    [Fact]
    public async Task GivenOversizedPageSize_WhenSearching_ThenItIsClampedToTheRecordedCap()
    {
        using var fixture = Serving("search/search-empty.json");

        await Client(fixture).SearchAsync(new IssueSearchQuery("project = GCM", 100000), null, CancellationToken.None);

        Assert.Contains("maxResults=5000", Assert.Single(fixture.Handler.Requests).PathAndQuery);
    }

    [Fact]
    public async Task GivenThreeRecordedPages_WhenFollowingCursors_ThenReachesLastPageWithSevenIssues()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            SearchPath,
            FakeResponse.Ok(FixtureFiles.Read("search/search-page1.json")),
            FakeResponse.Ok(FixtureFiles.Read("search/search-page2.json")),
            FakeResponse.Ok(FixtureFiles.Read("search/search-page3-last.json")));
        var query = new IssueSearchQuery("project in (GCM, GTM) ORDER BY created ASC", 3);
        var keys = new List<string>();
        PageCursor? cursor = null;
        var pages = 0;

        do
        {
            var result = await Client(fixture).SearchAsync(query, cursor, CancellationToken.None);
            keys.AddRange(result.Value.Items.Select(issue => issue.Key));
            cursor = result.Value.Next;
            pages++;
        }
        while (cursor is not null);

        Assert.Equal(3, pages);
        Assert.Equal(7, keys.Count);
        Assert.Equal(7, keys.Distinct().Count());
        var requests = fixture.Handler.Requests.ToList();
        Assert.DoesNotContain("nextPageToken", requests[0].PathAndQuery);
        Assert.Contains("nextPageToken=", requests[1].PathAndQuery);
        Assert.Contains("nextPageToken=", requests[2].PathAndQuery);
    }

    [Fact]
    public async Task GivenOlderFixtureWithFewFields_WhenMapping_ThenMissingFieldsGetNeutralValues()
    {
        using var fixture = Serving("search/search-page1.json");

        var result = await FirstPageAsync(fixture, "project = GCM");

        var first = result.Value.Items[0];
        Assert.Null(first.Priority);
        Assert.Null(first.Updated);
        Assert.Equal(string.Empty, first.ProjectKey);
        Assert.NotNull(result.Value.Next);
    }

    [Fact]
    public async Task GivenEmptyResult_WhenSearching_ThenPageIsEmptyAndLast()
    {
        using var fixture = Serving("search/search-empty.json");

        var result = await FirstPageAsync(fixture, "project = NOPE");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.True(result.Value.IsLast);
    }

    [Fact]
    public async Task GivenOddJson_WhenMapping_ThenAssigneeUnicodeAndUnknownFieldsAreHandled()
    {
        const string json = """
            {"isLast": true, "somethingNew": [1, 2], "issues": [
              {"id": "1", "key": "GCM-1", "newThing": {"a": 1}, "fields": {
                "summary": "Café 🚀 日本語", "assignee": {"accountId": "fake-account-9",
                "displayName": "Ünïcode Üser", "active": false}, "labels": ["a", "b"],
                "updated": "not a date", "status": {"id": "3", "name": "Wait", "statusCategory": {"key": "done"}},
                "issuetype": {"id": "7", "name": "Whatever", "hierarchyLevel": 0, "subtask": false},
                "unknownField": {"x": null}}},
              {"id": "2", "key": "GCM-2"},
              {"id": "3", "key": "GCM-3", "fields": {"status": null, "issuetype": null, "labels": null,
                "assignee": null, "parent": {"key": "GCM-1"}}}
            ]}
            """;
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.Ok(json));

        var result = await FirstPageAsync(fixture, "project = GCM");

        var items = result.Value.Items;
        Assert.Equal(3, items.Count);
        Assert.Equal("Café 🚀 日本語", items[0].Summary);
        Assert.Equal("Ünïcode Üser", items[0].Assignee?.DisplayName);
        Assert.Equal(["a", "b"], items[0].Labels);
        Assert.Null(items[0].Updated);
        Assert.Equal("done", items[0].Status.CategoryKey);
        Assert.Equal("Wait", items[0].Status.Name);
        Assert.Equal("3", items[0].Status.Id);
        Assert.Equal("1", items[0].Id);
        Assert.Equal("fake-account-9", items[0].Assignee?.AccountId);
        Assert.Equal("7", items[0].IssueType.Id);
        Assert.Equal("Whatever", items[0].IssueType.Name);
        Assert.Equal("undefined", items[1].Status.CategoryKey);
        Assert.Empty(items[1].Labels);
        Assert.Null(items[1].Parent);
        Assert.Equal("GCM-1", items[2].Parent?.Key);
        Assert.Null(items[2].Parent?.Summary);
        Assert.Null(items[2].Assignee);
    }

    [Fact]
    public async Task GivenVeryLongJql_WhenSearching_ThenPostsJsonBodyWithTokenAndFields()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.Ok(FixtureFiles.Read("search/search-post.json"));
        fixture.Handler.Serve(HttpMethod.Post, SearchPath, reply);
        var keys = string.Join(", ", Enumerable.Range(1, 400).Select(number => $"GCM-{number}"));
        var jql = $"key in ({keys}) ORDER BY created ASC";

        var result = await Client(fixture).SearchAsync(
            new IssueSearchQuery(jql, 40),
            new PageCursor("token-1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(SearchPath, request.PathAndQuery);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(jql, body.RootElement.GetProperty("jql").GetString());
        Assert.Equal(40, body.RootElement.GetProperty("maxResults").GetInt32());
        Assert.Equal("token-1", body.RootElement.GetProperty("nextPageToken").GetString());
        var fields = body.RootElement.GetProperty("fields").EnumerateArray().Select(item => item.GetString());
        Assert.Equal(IssueFields.ListRow, fields);
    }

    [Fact]
    public async Task GivenLongJqlOnFirstPage_WhenSearching_ThenBodyHasNoTokenProperty()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.Ok(FixtureFiles.Read("search/search-post.json"));
        fixture.Handler.Serve(HttpMethod.Post, SearchPath, reply);
        var jql = $"summary ~ \"{new string('x', 3000)}\"";

        await Client(fixture).SearchAsync(new IssueSearchQuery(jql), null, CancellationToken.None);

        using var body = JsonDocument.Parse(Assert.Single(fixture.Handler.Requests).Body!);
        Assert.False(body.RootElement.TryGetProperty("nextPageToken", out _));
    }

    [Fact]
    public async Task GivenBadJql_WhenSearching_ThenJiraMessageIsSurfacedAsBadRequest()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/search-400-bad-jql.json");
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await FirstPageAsync(fixture, "project =");

        Assert.Equal(JiraErrorKind.BadRequest, result.Error.Kind);
        Assert.StartsWith("Error in the JQL Query", result.Error.Message);
    }

    [Fact]
    public async Task GivenExpiredToken_WhenContinuing_ThenErrorSaysPageExpired()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/search-400-bad-token.json");
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await Client(fixture).SearchAsync(
            new IssueSearchQuery("project = GCM"),
            new PageCursor("old"),
            CancellationToken.None);

        Assert.Equal(JiraErrorKind.PageExpired, result.Error.Kind);
        Assert.Equal(400, result.Error.StatusCode);
        Assert.StartsWith("The page expired, reload", result.Error.Message);
        Assert.Contains("invalid or expired", result.Error.Message);
    }

    [Fact]
    public async Task GivenBadJqlWithCursor_WhenContinuing_ThenJiraMessageStaysVisible()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/search-400-bad-jql.json");
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await Client(fixture).SearchAsync(
            new IssueSearchQuery("project ="),
            new PageCursor("old"),
            CancellationToken.None);

        Assert.Contains("Error in the JQL Query", result.Error.Message);
    }

    [Fact]
    public async Task GivenOverCapPageSizeBody_WhenJiraRejects_ThenItIsAPlainBadRequest()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/search-400-maxresults-cap.json");
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.Equal(JiraErrorKind.BadRequest, result.Error.Kind);
        Assert.Contains("between 1 and 5,000", result.Error.Message);
    }

    [Theory]
    [InlineData(401, JiraErrorKind.Unauthorized)]
    [InlineData(403, JiraErrorKind.Forbidden)]
    [InlineData(410, JiraErrorKind.NotFound)]
    public async Task GivenFailureStatus_WhenSearching_ThenTypedErrorWithoutRetry(int status, JiraErrorKind expected)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus((HttpStatusCode)status));

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.Equal(expected, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenPersistentRateLimit_WhenSearching_ThenRetriesThenReportsRateLimited()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.RateLimited("2"));

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal(4, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenServerErrorThenPage_WhenSearching_ThenRetriesAndSucceeds()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(HttpStatusCode.BadGateway));
        var reply = FakeResponse.Ok(FixtureFiles.Read("search/search-empty.json"));
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, reply);

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenPersistentServerError_WhenSearching_ThenServerErrorIsReported()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.WithStatus(HttpStatusCode.InternalServerError));

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.Equal(JiraErrorKind.ServerError, result.Error.Kind);
    }

    [Fact]
    public async Task GivenHangingServer_WhenCallerCancels_ThenOperationCanceledIsThrown()
    {
        using var fixture = new TransportFixture();
        using var source = new CancellationTokenSource();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.Hanging());

        var pending = Client(fixture).SearchAsync(new IssueSearchQuery("project = GCM"), null, source.Token);
        await fixture.Handler.WaitForInFlightAsync(1);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task GivenMalformedBody_WhenSearching_ThenInvalidResponseNotAnException()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.Ok("{\"issues\": [ {"));

        var result = await FirstPageAsync(fixture, "project = GCM");

        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
    }

    [Fact]
    public async Task GivenCountFixture_WhenCounting_ThenPostsJqlAndReturnsCount()
    {
        using var fixture = new TransportFixture();
        var path = JiraSearchClient.ApproximateCountPath;
        var reply = FakeResponse.Ok(FixtureFiles.Read("search/approximate-count.json"));
        fixture.Handler.Serve(HttpMethod.Post, path, reply);

        var result = await Client(fixture).ApproximateCountAsync(
            new IssueSearchQuery("project = GCM"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value > 0);
        var request = Assert.Single(fixture.Handler.Requests);
        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("project = GCM", body.RootElement.GetProperty("jql").GetString());
    }

    [Fact]
    public void GivenQuery_WhenMarkedBackground_ThenPriorityIsBackgroundAndOriginalUnchanged()
    {
        var query = new IssueSearchQuery("project = GCM");

        var background = query.AsBackground();

        Assert.Equal(JiraPriority.Background, background.Priority);
        Assert.Equal(JiraPriority.User, query.Priority);
    }

    [Fact]
    public void GivenOffsetPages_WhenAskingForNext_ThenOnlyNonLastPagesHaveAnOffset()
    {
        var middle = new OffsetPage<int>([1, 2, 3], 10, 3, 40, false);
        var last = new OffsetPage<int>([1], 39, 3, 40, true);

        Assert.Equal(3, middle.PageSize);
        Assert.Equal(40, middle.Total);
        Assert.Equal(13, middle.NextStartAt);
        Assert.Null(last.NextStartAt);
    }

    private static Task<JiraResult<IssuePage>> FirstPageAsync(TransportFixture fixture, string jql) =>
        Client(fixture).SearchAsync(new IssueSearchQuery(jql), null, CancellationToken.None);

    private static JiraSearchClient Client(TransportFixture fixture) => new(fixture.Transport);

    private static TransportFixture Serving(string fixtureName)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, SearchPath, FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        return fixture;
    }
}
