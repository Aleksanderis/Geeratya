using System.Collections.Specialized;
using System.Net;
using System.Web;
using Geeratya.Jira.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Boards;

public sealed class BoardClientTests
{
    private const string GcmIssuesPath = "/rest/agile/1.0/board/4/issue";

    [Fact]
    public async Task GivenCompanyManagedConfiguration_WhenLoading_ThenColumnsKeepOrderAndExtrasAreMapped()
    {
        using var fixture = Serving("/rest/agile/1.0/board/4/configuration", "boards/board-gcm-config.json");

        var result = await new BoardClient(fixture.Transport).GetConfigurationAsync(4, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var configuration = result.Value;
        Assert.Equal(BoardType.Kanban, configuration.Type);
        Assert.Equal(["Backlog", "To Do", "In Progress", "Done"], configuration.Columns.Select(c => c.Name));
        Assert.Equal(["10003"], configuration.Columns[0].StatusIds);
        Assert.Equal("10001", configuration.FilterId);
        Assert.Equal("fixVersion in unreleasedVersions() OR fixVersion is EMPTY", configuration.SubQuery);
        Assert.Equal("customfield_10019", configuration.RankFieldId);
        Assert.Equal("issueCount", configuration.ConstraintType);
    }

    [Fact]
    public async Task GivenTeamManagedConfiguration_WhenLoading_ThenBacklogIsThirdAndSubQueryIsAbsent()
    {
        using var fixture = Serving("/rest/agile/1.0/board/3/configuration", "boards/board-gtm-config.json");

        var result = await new BoardClient(fixture.Transport).GetConfigurationAsync(3, CancellationToken.None);

        var configuration = result.Value;
        Assert.Equal(BoardType.Simple, configuration.Type);
        Assert.Equal(["To Do", "In Progress", "Backlog", "Done"], configuration.Columns.Select(c => c.Name));
        Assert.Null(configuration.SubQuery);
        Assert.Equal("customfield_10019", configuration.RankFieldId);
    }

    [Fact]
    public async Task GivenOddConfigurationJson_WhenMapping_ThenMissingPartsBecomeNeutralValues()
    {
        const string json = """{"id": 7, "type": "hologram", "columnConfig": {"columns": [{"name": "A"}]}, "x": [1]}""";
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, "/rest/agile/1.0/board/7/configuration", FakeResponse.Ok(json));

        var result = await new BoardClient(fixture.Transport).GetConfigurationAsync(7, CancellationToken.None);

        var configuration = result.Value;
        Assert.Equal(BoardType.Unknown, configuration.Type);
        Assert.Empty(configuration.Columns[0].StatusIds);
        Assert.Equal(string.Empty, configuration.FilterId);
        Assert.Null(configuration.RankFieldId);
        Assert.Null(configuration.ConstraintType);
    }

    [Fact]
    public async Task GivenBoardListFixture_WhenListing_ThenBoardsOfBothKindsAreMappedAndPageIsLast()
    {
        using var fixture = Serving("/rest/agile/1.0/board", "boards/board-list.json");

        var result = await new BoardClient(fixture.Transport)
            .ListBoardsAsync(new BoardListQuery(), 0, CancellationToken.None);

        var page = result.Value;
        Assert.True(page.IsLast);
        Assert.Equal(2, page.Total);
        Assert.Equal([BoardType.Simple, BoardType.Kanban], page.Items.Select(board => board.Type));
        Assert.Equal(["GTM", "GCM"], page.Items.Select(board => board.ProjectKey));
        Assert.Equal(4, page.Items[1].Id);
    }

    [Fact]
    public async Task GivenFilters_WhenListing_ThenProjectAndTypeAreSentEscaped()
    {
        using var fixture = Serving("/rest/agile/1.0/board", "boards/board-list-filtered.json");

        await new BoardClient(fixture.Transport).ListBoardsAsync(
            new BoardListQuery("G&M", BoardType.Kanban, 100000),
            -5,
            CancellationToken.None);

        var query = QueryOf(Assert.Single(fixture.Handler.Requests));
        Assert.Equal("G&M", query["projectKeyOrId"]);
        Assert.Equal("kanban", query["type"]);
        Assert.Equal("0", query["startAt"]);
        Assert.Equal("5000", query["maxResults"]);
    }

    [Fact]
    public async Task GivenBoardFixture_WhenGettingBoard_ThenSummaryIsMapped()
    {
        using var fixture = Serving("/rest/agile/1.0/board/3", "boards/board-gtm.json");

        var result = await new BoardClient(fixture.Transport).GetBoardAsync(3, CancellationToken.None);

        Assert.Equal(new BoardSummary(3, "GTM board", BoardType.Simple, "GTM"), result.Value);
    }

    [Fact]
    public async Task GivenCompanyManagedBoard_WhenLoadingIssues_ThenAllRowsMapAndPageEndsAtTotal()
    {
        using var fixture = Serving(GcmIssuesPath, "boards/rows-gcm-issues.json");

        var result = await Issues(fixture, 4, null);

        var page = result.Value;
        Assert.Equal(24, page.Items.Count);
        Assert.Equal(24, page.Total);
        Assert.True(page.IsLast);
        var subtask = page.Items.Single(issue => issue.Key == "GCM-6");
        Assert.True(subtask.IssueType.IsSubtask);
        Assert.Equal("GCM-1", subtask.Parent?.Key);
    }

    [Fact]
    public async Task GivenTeamManagedBoard_WhenLoadingIssuesAndBacklog_ThenBacklogIsTheEndpointsAnswerNotAColumn()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/agile/1.0/board/3/issue",
            FakeResponse.Ok(FixtureFiles.Read("boards/rows-gtm-issues.json")));
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/agile/1.0/board/3/backlog",
            FakeResponse.Ok(FixtureFiles.Read("boards/rows-gtm-backlog.json")));
        var client = new BoardClient(fixture.Transport);

        var all = await client.GetIssuesAsync(3, new BoardIssueQuery(), null, CancellationToken.None);
        var backlog = await client.GetBacklogAsync(3, new BoardIssueQuery(), null, CancellationToken.None);

        Assert.Equal(16, all.Value.Items.Count);
        Assert.Equal(13, backlog.Value.Items.Count);
        Assert.True(backlog.Value.Items.All(issue => issue.ProjectKey == "GTM"));
        Assert.Contains(backlog.Value.Items, issue => issue.IssueType.IsSubtask);
        Assert.Equal(
            "/rest/agile/1.0/board/3/backlog",
            fixture.Handler.Requests.Last().PathAndQuery.Split('?')[0]);
    }

    [Fact]
    public async Task GivenCompanyManagedBacklog_WhenLoading_ThenOnlyTheEndpointsIssuesAreReturned()
    {
        using var fixture = Serving("/rest/agile/1.0/board/4/backlog", "boards/rows-gcm-backlog.json");

        var result = await new BoardClient(fixture.Transport)
            .GetBacklogAsync(4, new BoardIssueQuery(), null, CancellationToken.None);

        Assert.Equal(12, result.Value.Items.Count);
        Assert.Equal(12, result.Value.Total);
    }

    [Fact]
    public async Task GivenThreeRecordedPages_WhenFollowingCursors_ThenWalkEndsAtTotalWithTwentyFourDistinctIssues()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            GcmIssuesPath,
            FakeResponse.Ok(FixtureFiles.Read("boards/rows-gcm-issues-page1.json")),
            FakeResponse.Ok(FixtureFiles.Read("boards/rows-gcm-issues-page2.json")),
            FakeResponse.Ok(FixtureFiles.Read("boards/rows-gcm-issues-page3-last.json")));
        var keys = new List<string>();
        PageCursor? cursor = null;

        do
        {
            var result = await Issues(fixture, 4, cursor, 10);
            keys.AddRange(result.Value.Items.Select(issue => issue.Key));
            cursor = result.Value.Next;
        }
        while (cursor is not null);

        Assert.Equal(24, keys.Distinct().Count());
        var starts = fixture.Handler.Requests.Select(request => QueryOf(request)["startAt"]).ToList();
        Assert.Equal(["0", "10", "20"], starts);
    }

    [Fact]
    public async Task GivenQuery_WhenLoadingIssues_ThenFieldsPageSizeAndJqlAreSentAndSelfLinksIgnored()
    {
        using var fixture = Serving(GcmIssuesPath, "boards/rows-gcm-issues-jql-done.json");
        const string jql = "status = \"Done\" AND summary ~ \"a&b\"";

        var result = await new BoardClient(fixture.Transport).GetIssuesAsync(
            4,
            new BoardIssueQuery(70000, jql),
            PageCursor.Offset(30),
            CancellationToken.None);

        Assert.Equal(3, result.Value.Items.Count);
        var query = QueryOf(Assert.Single(fixture.Handler.Requests));
        Assert.Equal(IssueFields.ListRowQueryValue, query["fields"]);
        Assert.Equal("30", query["startAt"]);
        Assert.Equal("5000", query["maxResults"]);
        Assert.Equal(jql, query["jql"]);
    }

    [Fact]
    public async Task GivenSearchTokenCursor_WhenLoadingIssues_ThenItFailsWithoutCallingJira()
    {
        using var fixture = new TransportFixture();

        var result = await Issues(fixture, 4, new PageCursor("abc=="));

        Assert.Equal(JiraErrorKind.BadRequest, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenEmptyPageThoughTotalIsLarger_WhenMapping_ThenWalkEnds()
    {
        const string json = """{"startAt": 100, "maxResults": 100, "total": 500, "issues": []}""";
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, GcmIssuesPath, FakeResponse.Ok(json));

        var result = await Issues(fixture, 4, PageCursor.Offset(100));

        Assert.True(result.Value.IsLast);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "boards/board-issues-bad-jql-400.json", JiraErrorKind.BadRequest)]
    [InlineData(HttpStatusCode.NotFound, "boards/board-404.json", JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, null, JiraErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, null, JiraErrorKind.Forbidden)]
    public async Task GivenErrorReply_WhenLoadingIssues_ThenTypedErrorIsReturned(
        HttpStatusCode status,
        string? fixtureName,
        JiraErrorKind expected)
    {
        using var fixture = new TransportFixture();
        var body = fixtureName is null ? string.Empty : FixtureFiles.Read(fixtureName);
        fixture.Handler.Serve(HttpMethod.Get, GcmIssuesPath, FakeResponse.WithStatus(status, body));

        var result = await Issues(fixture, 4, null);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error.Kind);
    }

    [Fact]
    public async Task GivenBadJqlReply_WhenLoadingIssues_ThenJiraMessageIsKept()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus(
            HttpStatusCode.BadRequest,
            FixtureFiles.Read("boards/board-issues-bad-jql-400.json"));
        fixture.Handler.Serve(HttpMethod.Get, GcmIssuesPath, reply);

        var result = await Issues(fixture, 4, null);

        Assert.Contains("quoted string has not been completed", result.Error.Message, StringComparison.Ordinal);
    }

    private static Task<JiraResult<IssuePage>> Issues(
        TransportFixture fixture,
        long boardId,
        PageCursor? cursor,
        int pageSize = BoardIssueQuery.DefaultPageSize) =>
        new BoardClient(fixture.Transport)
            .GetIssuesAsync(boardId, new BoardIssueQuery(pageSize), cursor, CancellationToken.None);

    private static NameValueCollection QueryOf(RecordedRequest request) =>
        HttpUtility.ParseQueryString(new Uri($"https://x{request.PathAndQuery}").Query);

    private static TransportFixture Serving(string path, string fixtureName)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(FixtureFiles.Read(fixtureName)));

        return fixture;
    }
}
