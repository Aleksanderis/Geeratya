using System.Collections.Specialized;
using System.Net;
using System.Web;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Sprints;

public sealed class SprintClientTests
{
    private const string CompanySprintsPath = "/rest/agile/1.0/board/6/sprint";
    private const string TeamSprintsPath = "/rest/agile/1.0/board/5/sprint";

    [Fact]
    public async Task GivenCompanyManagedSprints_WhenListing_ThenAllStatesAndDatesAreMappedInJiraOrder()
    {
        using var fixture = Serving(CompanySprintsPath, "scrum/sprints-gcs.json");

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(6, new SprintQuery(), 0, CancellationToken.None);

        var page = result.Value;
        Assert.True(page.IsLast);
        Assert.Equal([6L, 7L, 2L, 8L], page.Items.Select(sprint => sprint.Id));
        Assert.Equal(
            [SprintState.Closed, SprintState.Active, SprintState.Future, SprintState.Future],
            page.Items.Select(sprint => sprint.State));
        var closed = page.Items[0];
        Assert.Equal("Seed Sprint 1", closed.Name);
        Assert.Equal(6, closed.OriginBoardId);
        Assert.Equal(new DateTimeOffset(2026, 11, 2, 17, 21, 0, 60, TimeSpan.Zero), closed.CompleteDate);
        Assert.Equal(new DateTimeOffset(2026, 10, 19, 17, 20, 59, TimeSpan.Zero), closed.StartDate);
        Assert.Null(page.Items[1].CompleteDate);
    }

    [Fact]
    public async Task GivenSprintWithoutDatesOrGoal_WhenListing_ThenDatesAndGoalAreNull()
    {
        using var fixture = Serving(CompanySprintsPath, "scrum/sprints-gcs.json");

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(6, new SprintQuery(), 0, CancellationToken.None);

        var neverStarted = result.Value.Items.Single(sprint => sprint.Id == 2);
        Assert.Null(neverStarted.StartDate);
        Assert.Null(neverStarted.EndDate);
        Assert.Null(neverStarted.CompleteDate);
        Assert.Null(neverStarted.Goal);
    }

    [Fact]
    public async Task GivenOddSprintJson_WhenListing_ThenMissingPartsBecomeNeutralValues()
    {
        const string json = """
            {"values": [{"id": 3, "state": "paused", "goal": "  ", "startDate": "soon", "x": [1]}, {}]}
            """;
        using var fixture = ServingJson(CompanySprintsPath, json);

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(6, new SprintQuery(), 0, CancellationToken.None);

        var items = result.Value.Items;
        Assert.Equal(SprintState.Unknown, items[0].State);
        Assert.Null(items[0].Goal);
        Assert.Null(items[0].StartDate);
        Assert.Equal(string.Empty, items[0].Name);
        Assert.Equal(0, items[1].Id);
        Assert.False(result.Value.IsLast);
    }

    [Fact]
    public async Task GivenStatesInQuery_WhenListing_ThenStateParameterIsCommaSeparatedAndSizeClamped()
    {
        using var fixture = Serving(CompanySprintsPath, "scrum/sprints-gcs-active-future.json");
        var query = new SprintQuery([SprintState.Active, SprintState.Unknown, SprintState.Future], 1000);

        var result = await new SprintClient(fixture.Transport).ListSprintsAsync(6, query, -3, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var parameters = QueryOf(Assert.Single(fixture.Handler.Requests));
        Assert.Equal("active,future", parameters["state"]);
        Assert.Equal("50", parameters["maxResults"]);
        Assert.Equal("0", parameters["startAt"]);
    }

    [Fact]
    public async Task GivenNoStates_WhenListing_ThenNoStateParameterIsSent()
    {
        using var fixture = Serving(CompanySprintsPath, "scrum/sprints-gcs.json");

        await new SprintClient(fixture.Transport).ListSprintsAsync(6, new SprintQuery([]), 0, CancellationToken.None);

        Assert.Null(QueryOf(Assert.Single(fixture.Handler.Requests))["state"]);
    }

    [Fact]
    public async Task GivenTeamManagedFirstPage_WhenListing_ThenPageIsNotLastAndNextStartAtIsCount()
    {
        using var fixture = Serving(TeamSprintsPath, "scrum/sprints-gts-page1.json");

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(5, new SprintQuery(), 0, CancellationToken.None);

        var page = result.Value;
        Assert.False(page.IsLast);
        Assert.Equal(page.Items.Count, page.NextStartAt);
        Assert.Equal(SprintState.Closed, page.Items[0].State);
    }

    [Fact]
    public async Task GivenStartAtPastEnd_WhenListing_ThenPageIsEmptyAndLast()
    {
        using var fixture = Serving(CompanySprintsPath, "scrum/sprints-gcs-startat-past-end.json");

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(6, new SprintQuery(), 99, CancellationToken.None);

        Assert.Empty(result.Value.Items);
        Assert.True(result.Value.IsLast);
    }

    [Theory]
    [InlineData("scrum/error-sprints-gcm-kanban-400.json", HttpStatusCode.BadRequest, JiraErrorKind.BadRequest)]
    [InlineData("scrum/error-sprints-board-404.json", HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    public async Task GivenErrorReply_WhenListing_ThenTypedErrorIsReturned(
        string fixtureName,
        HttpStatusCode status,
        JiraErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            CompanySprintsPath,
            FakeResponse.WithStatus(status, FixtureFiles.Read(fixtureName)));

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(6, new SprintQuery(), 0, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenRateLimitedReply_WhenListing_ThenRateLimitedError()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, CompanySprintsPath, FakeResponse.RateLimited("0"));

        var result = await new SprintClient(fixture.Transport)
            .ListSprintsAsync(6, new SprintQuery(), 0, CancellationToken.None);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
    }

    [Fact]
    public async Task GivenClosedSprintFixture_WhenGetting_ThenSprintIsMapped()
    {
        using var fixture = Serving("/rest/agile/1.0/sprint/6", "scrum/sprint-gcs-closed.json");

        var result = await new SprintClient(fixture.Transport).GetSprintAsync(6, CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.Value.State);
        Assert.Equal("Seed Sprint 1", result.Value.Name);
        Assert.NotNull(result.Value.CompleteDate);
    }

    [Fact]
    public async Task GivenTeamManagedSprintFixture_WhenGetting_ThenSprintIsMapped()
    {
        using var fixture = Serving("/rest/agile/1.0/sprint/9", "scrum/sprint-gts-closed.json");

        var result = await new SprintClient(fixture.Transport).GetSprintAsync(9, CancellationToken.None);

        Assert.Equal(SprintState.Closed, result.Value.State);
        Assert.Equal(5, result.Value.OriginBoardId);
    }

    [Fact]
    public async Task GivenUnknownSprint_WhenGetting_ThenNotFound()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            "/rest/agile/1.0/sprint/99",
            FakeResponse.WithStatus(HttpStatusCode.NotFound, FixtureFiles.Read("scrum/error-sprint-404.json")));

        var result = await new SprintClient(fixture.Transport).GetSprintAsync(99, CancellationToken.None);

        Assert.Equal(JiraErrorKind.NotFound, result.Error.Kind);
    }

    [Fact]
    public async Task GivenOneActiveSprint_WhenGettingActive_ThenOnlyThatSprintAndStateFilterIsSent()
    {
        using var fixture = Serving(CompanySprintsPath, "scrum/sprints-gcs-active.json");

        var result = await new SprintClient(fixture.Transport).GetActiveSprintsAsync(6, CancellationToken.None);

        var sprint = Assert.Single(result.Value);
        Assert.Equal(7, sprint.Id);
        Assert.Equal("active", QueryOf(Assert.Single(fixture.Handler.Requests))["state"]);
    }

    [Fact]
    public async Task GivenNoActiveSprint_WhenGettingActive_ThenListIsEmpty()
    {
        using var fixture = ServingJson(CompanySprintsPath, """{"values": [], "startAt": 0, "isLast": true}""");

        var result = await new SprintClient(fixture.Transport).GetActiveSprintsAsync(6, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GivenTwoActiveSprintsOnTwoPages_WhenGettingActive_ThenBothAreReturnedInOrder()
    {
        const string first = """
            {"startAt": 0, "maxResults": 1, "total": 2, "values": [{"id": 7, "state": "active", "name": "A"}]}
            """;
        const string second = """
            {"startAt": 1, "maxResults": 1, "total": 2, "values": [{"id": 12, "state": "active", "name": "B"}]}
            """;
        using var fixture = new TransportFixture();
        fixture.Handler.Script(HttpMethod.Get, CompanySprintsPath, FakeResponse.Ok(first), FakeResponse.Ok(second));

        var result = await new SprintClient(fixture.Transport).GetActiveSprintsAsync(6, CancellationToken.None);

        Assert.Equal([7L, 12L], result.Value.Select(sprint => sprint.Id));
        Assert.Equal("1", QueryOf(fixture.Handler.Requests.Last())["startAt"]);
    }

    [Fact]
    public async Task GivenReplyThatNeverEnds_WhenGettingActive_ThenWalkStopsWithInvalidResponse()
    {
        using var fixture = ServingJson(CompanySprintsPath, """{"values": [{"id": 1, "state": "active"}]}""");

        var result = await new SprintClient(fixture.Transport).GetActiveSprintsAsync(6, CancellationToken.None);

        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
        Assert.Equal(20, fixture.Handler.Requests.Count);
        Assert.Equal("19", QueryOf(fixture.Handler.Requests.Last())["startAt"]);
    }

    [Fact]
    public async Task GivenFailureOnSecondPage_WhenGettingActive_ThenWholeCallFails()
    {
        const string first = """{"startAt": 0, "total": 2, "values": [{"id": 7, "state": "active"}]}""";
        using var fixture = new TransportFixture();
        fixture.Handler.Script(
            HttpMethod.Get,
            CompanySprintsPath,
            FakeResponse.Ok(first),
            FakeResponse.WithStatus(HttpStatusCode.Forbidden, "{}"));

        var result = await new SprintClient(fixture.Transport).GetActiveSprintsAsync(6, CancellationToken.None);

        Assert.Equal(JiraErrorKind.Forbidden, result.Error.Kind);
    }

    [Fact]
    public async Task GivenSecondPageOfSprintIssues_WhenLoading_ThenRowsAreMappedAndNextAdvancesByCount()
    {
        using var fixture = Serving("/rest/agile/1.0/sprint/7/issue", "scrum/sprint-issues-gcs-active-page2.json");

        var result = await new SprintClient(fixture.Transport)
            .GetIssuesAsync(7, new BoardIssueQuery(2), PageCursor.Offset(2), CancellationToken.None);

        var page = result.Value;
        Assert.Equal(7, page.Total);
        Assert.NotEmpty(page.Items);
        Assert.True(page.Next!.TryGetOffset(out var next));
        Assert.Equal(2 + page.Items.Count, next);
        var parameters = QueryOf(Assert.Single(fixture.Handler.Requests));
        Assert.Equal("2", parameters["startAt"]);
        Assert.Equal("2", parameters["maxResults"]);
        Assert.False(string.IsNullOrEmpty(parameters["fields"]));
    }

    [Fact]
    public async Task GivenEmptySprint_WhenLoadingIssues_ThenPageIsEmptyAndLast()
    {
        using var fixture = Serving("/rest/agile/1.0/sprint/2/issue", "scrum/sprint-issues-gcs-empty.json");

        var result = await new SprintClient(fixture.Transport)
            .GetIssuesAsync(2, new BoardIssueQuery(), null, CancellationToken.None);

        Assert.Empty(result.Value.Items);
        Assert.Null(result.Value.Next);
    }

    [Fact]
    public async Task GivenTeamManagedSprint_WhenLoadingIssues_ThenIssuesAreMapped()
    {
        using var fixture = Serving("/rest/agile/1.0/sprint/10/issue", "scrum/sprint-issues-gts-active.json");

        var result = await new SprintClient(fixture.Transport)
            .GetIssuesAsync(10, new BoardIssueQuery(), null, CancellationToken.None);

        Assert.NotEmpty(result.Value.Items);
        Assert.All(result.Value.Items, issue => Assert.StartsWith("GTS-", issue.Key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task GivenJqlInQuery_WhenLoadingIssues_ThenItIsEscapedIntoTheRequest()
    {
        using var fixture = Serving("/rest/agile/1.0/sprint/7/issue", "scrum/sprint-issues-gcs-active-jql.json");
        var query = new BoardIssueQuery(100, "status = \"Done\" & x");

        var result = await new SprintClient(fixture.Transport).GetIssuesAsync(7, query, null, CancellationToken.None);

        Assert.Equal(3, result.Value.Total);
        Assert.Equal("status = \"Done\" & x", QueryOf(Assert.Single(fixture.Handler.Requests))["jql"]);
    }

    [Fact]
    public async Task GivenTokenCursor_WhenLoadingIssues_ThenBadRequestWithoutSendingAnything()
    {
        using var fixture = new TransportFixture();

        var result = await new SprintClient(fixture.Transport)
            .GetIssuesAsync(7, new BoardIssueQuery(), new PageCursor("abc"), CancellationToken.None);

        Assert.Equal(JiraErrorKind.BadRequest, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    private static NameValueCollection QueryOf(RecordedRequest request) =>
        HttpUtility.ParseQueryString(new Uri($"https://x{request.PathAndQuery}").Query);

    private static TransportFixture Serving(string path, string fixtureName) =>
        ServingJson(path, FixtureFiles.Read(fixtureName));

    private static TransportFixture ServingJson(string path, string json)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, path, FakeResponse.Ok(json));

        return fixture;
    }
}
