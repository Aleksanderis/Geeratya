using System.Net;
using Geeratya.Jira.Gateway;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Gateway;

public sealed class BoardViewClientTests
{
    private const string ViewMarker = ":view/activation/";

    [Fact]
    public async Task GivenTenantInfo_WhenReadingTheCloudId_ThenItIsReturnedFromAGetWithoutABody()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Ok(Gateway("tenant-info")));

        var result = await new BoardViewClient(fixture.Transport).GetCloudIdAsync(CancellationToken.None);

        Assert.Equal("00000000-0000-0000-0000-000000000000", result.Value);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Null(request.Body);
    }

    [Fact]
    public async Task GivenTenantInfoWithoutCloudId_WhenReading_ThenItIsAnInvalidResponse()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Ok("{}"));

        var result = await new BoardViewClient(fixture.Transport).GetCloudIdAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
    }

    [Theory]
    [InlineData("navigation-gcm4", 4, "/board/4/board")]
    [InlineData("navigation-gtm3", 3, "/board/3/board")]
    [InlineData("navigation-gcs6", 6, "/board/6/backlog")]
    public async Task GivenRecordedNavigation_WhenResolvingTheView_ThenTheNavigationItemIdBecomesTheViewId(
        string fixtureName,
        long boardId,
        string tail)
    {
        using var fixture = Posting(Gateway(fixtureName));

        var result = await new BoardViewClient(fixture.Transport).GetViewIdAsync("cloud-1", boardId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("ari:cloud:jira:", result.Value);
        Assert.Contains(ViewMarker, result.Value);
        Assert.DoesNotContain("navigation-item", result.Value);
        Assert.EndsWith(tail, result.Value);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(BoardViewClient.GraphQlPath, request.PathAndQuery);
        Assert.Contains($"ari:cloud:jira-software:cloud-1:board/{boardId}", request.Body);
    }

    [Theory]
    [InlineData("cutoff-gcm4", 14)]
    [InlineData("cutoff-gtm3", 14)]
    public async Task GivenRecordedCutoff_WhenReading_ThenTheDaysAreReturnedForBothProjectKinds(
        string fixtureName,
        int days)
    {
        using var fixture = Posting(Gateway(fixtureName));

        var result = await new BoardViewClient(fixture.Transport).GetCutoffAsync("view-id", CancellationToken.None);

        Assert.Equal(days, result.Value.Days);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Contains("\"id\":\"view-id\"", request.Body);
        Assert.Contains("completedIssueSearchCutOffInDays", request.Body);
    }

    [Fact]
    public async Task GivenAnExplicitNull_WhenReadingTheCutoff_ThenThereIsNoCutoffAndItIsASuccess()
    {
        using var fixture = Posting("""{"data":{"node":{"completedIssueSearchCutOffInDays":null}}}""");

        var result = await new BoardViewClient(fixture.Transport).GetCutoffAsync("view-id", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Days);
    }

    [Theory]
    [InlineData("cutoff-gcs6-backlog", "a Scrum backlog view (the node has no such field)")]
    public async Task GivenAViewWithoutTheField_WhenReadingTheCutoff_ThenItIsAnInvalidResponse(
        string fixtureName,
        string because)
    {
        using var fixture = Posting(Gateway(fixtureName));

        var result = await new BoardViewClient(fixture.Transport).GetCutoffAsync("view-id", CancellationToken.None);

        Assert.False(result.IsSuccess, because);
        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
    }

    [Theory]
    [InlineData("""{"data":{"node":null}}""")]
    [InlineData("""{"data":null}""")]
    [InlineData("""{}""")]
    [InlineData("""{"data":{"node":{"completedIssueSearchCutOffInDays":"14"}}}""")]
    [InlineData("""{"data":{"node":{"completedIssueSearchCutOffInDays":0}}}""")]
    [InlineData("""{"data":{"node":{"completedIssueSearchCutOffInDays":-3}}}""")]
    [InlineData("""{"data":{"node":{"completedIssueSearchCutOffInDays":7.5}}}""")]
    [InlineData("""{"data":{"node":{"completedIssueSearchCutOffInDays":14}},"errors":[{"message":"boom"}]}""")]
    [InlineData("""not json""")]
    public async Task GivenASchemaChange_WhenReadingTheCutoff_ThenItIsAnInvalidResponseNotAnException(string body)
    {
        using var fixture = Posting(body);

        var result = await new BoardViewClient(fixture.Transport).GetCutoffAsync("view-id", CancellationToken.None);

        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
    }

    [Theory]
    [InlineData("""{"data":{"jira_boardsByIds":[]}}""")]
    [InlineData("""{"data":{"jira_boardsByIds":[null]}}""")]
    [InlineData("""{"data":{"jira_boardsByIds":[{"defaultNavigationItem":null}]}}""")]
    [InlineData("""{"data":{"jira_boardsByIds":[{"defaultNavigationItem":{"id":"ari:cloud:jira:c:other/1"}}]}}""")]
    [InlineData("""{"data":{"jira_boardsByIds":[{"defaultNavigationItem":{"id":"https://elsewhere/navigation-item/"}}]}}""")]
    [InlineData("""{"errors":[{"message":"Validation error"}]}""")]
    public async Task GivenASchemaChange_WhenResolvingTheView_ThenItIsAnInvalidResponse(string body)
    {
        using var fixture = Posting(body);

        var result = await new BoardViewClient(fixture.Transport).GetViewIdAsync("cloud-1", 4, CancellationToken.None);

        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, JiraErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden, JiraErrorKind.Forbidden)]
    [InlineData(HttpStatusCode.NotFound, JiraErrorKind.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError, JiraErrorKind.ServerError)]
    [InlineData(HttpStatusCode.BadGateway, JiraErrorKind.ServerError)]
    public async Task GivenAnHttpFailure_WhenReadingAnyLink_ThenTheTypedErrorComesBack(
        HttpStatusCode status,
        JiraErrorKind kind)
    {
        using var fixture = new TransportFixture(options => options with { MaxRetries = 0 });
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.WithStatus(status));
        fixture.Handler.Serve(HttpMethod.Post, BoardViewClient.GraphQlPath, FakeResponse.WithStatus(status));
        var client = new BoardViewClient(fixture.Transport);

        var tenant = await client.GetCloudIdAsync(CancellationToken.None);
        var view = await client.GetViewIdAsync("cloud-1", 4, CancellationToken.None);
        var cutoff = await client.GetCutoffAsync("view-id", CancellationToken.None);

        Assert.Equal(kind, tenant.Error.Kind);
        Assert.Equal(kind, view.Error.Kind);
        Assert.Equal(kind, cutoff.Error.Kind);
    }

    [Fact]
    public async Task GivenRateLimiting_WhenReadingTheCutoff_ThenTheRetryAfterIsReportedAfterTheRetriesRunOut()
    {
        using var fixture = new TransportFixture(options => options with { MaxRetries = 1 });
        fixture.Handler.Serve(HttpMethod.Post, BoardViewClient.GraphQlPath, FakeResponse.RateLimited("2"));

        var result = await new BoardViewClient(fixture.Transport).GetCutoffAsync("view-id", CancellationToken.None);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Error.RetryAfter);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenAnyCall_WhenRequesting_ThenOnlyReadQueriesAreSentNeverAMutation()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Ok(Gateway("tenant-info")));
        fixture.Handler.Script(
            HttpMethod.Post,
            BoardViewClient.GraphQlPath,
            FakeResponse.Ok(Gateway("navigation-gcm4")),
            FakeResponse.Ok(Gateway("cutoff-gcm4")));
        var client = new BoardViewClient(fixture.Transport);

        await client.GetCloudIdAsync(CancellationToken.None);
        await client.GetViewIdAsync("cloud-1", 4, CancellationToken.None);
        await client.GetCutoffAsync("view-id", CancellationToken.None);

        var documents = fixture.Handler.Requests.Where(request => request.Body is not null).ToList();
        Assert.Equal(2, documents.Count);
        Assert.All(documents, request => Assert.StartsWith("{\"query\":\"query ", request.Body));
        Assert.All(
            fixture.Handler.Requests,
            request => Assert.DoesNotContain("mutation", request.Body ?? string.Empty, StringComparison.OrdinalIgnoreCase));
        Assert.All(
            fixture.Handler.Requests,
            request => Assert.DoesNotContain("RemoveResolvedItemsSetting", request.Body ?? string.Empty));
    }

    [Fact]
    public async Task GivenACall_WhenRequesting_ThenTheTokenIsOnlyInTheAuthorizationHeader()
    {
        using var fixture = Posting(Gateway("cutoff-gcm4"));

        await new BoardViewClient(fixture.Transport).GetCutoffAsync("view-id", CancellationToken.None);

        var request = Assert.Single(fixture.Handler.Requests);
        Assert.DoesNotContain(TransportFixture.Token, request.Body);
        Assert.Equal(TransportFixture.Host, request.Host);
    }

    private static string Gateway(string name)
    {
        return FixtureFiles.Read($"gateway/{name}.json");
    }

    private static TransportFixture Posting(string body)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, BoardViewClient.GraphQlPath, FakeResponse.Ok(body));

        return fixture;
    }
}
