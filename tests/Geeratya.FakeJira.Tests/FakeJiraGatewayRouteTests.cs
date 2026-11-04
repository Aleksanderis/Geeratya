using System.Net;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraGatewayRouteTests : IDisposable
{
    private const string NavigationQuery = "query N($ids: [ID!]!) { jira_boardsByIds(ids: $ids) { x } }";
    private const string CutoffQuery = "query C($id: ID!) { node(id: $id) { x } }";

    private readonly HttpClient _client = new();
    private readonly FakeJiraServer _server;
    private readonly string _baseUrl;

    public FakeJiraGatewayRouteTests()
    {
        var options = FakeJiraOptionsParser.TryParse([], out var error);
        Assert.True(options is not null, error);
        _server = new FakeJiraServer(options);
        _baseUrl = _server.Start();
    }

    [Fact]
    public async Task GivenTheTenantLookup_WhenRead_ThenACloudIdComesBack()
    {
        var reply = await _client.GetStringAsync($"{_baseUrl}{FakeGateway.TenantPath}");

        Assert.Equal("00000000-0000-0000-0000-000000000000", JsonDocument.Parse(reply).RootElement.GetProperty("cloudId").GetString());
    }

    [Theory]
    [InlineData(4, "/board/4/board", 14)]
    [InlineData(3, "/board/3/board", 14)]
    public async Task GivenAKanbanBoard_WhenTheChainIsQueried_ThenTheViewCarriesTheRecordedCutoff(
        int board,
        string tail,
        int days)
    {
        var navigation = await Query(NavigationQuery, $$"""{"ids":["ari:cloud:jira-software:c:board/{{board}}"]}""");
        var viewId = navigation.GetProperty("data").GetProperty("jira_boardsByIds")[0]
            .GetProperty("defaultNavigationItem").GetProperty("id").GetString()!
            .Replace(":navigation-item/", ":view/", StringComparison.Ordinal);

        var cutoff = await Query(CutoffQuery, $$"""{"id":"{{viewId}}"}""");

        Assert.EndsWith(tail, viewId);
        Assert.Equal(days, cutoff.GetProperty("data").GetProperty("node").GetProperty("completedIssueSearchCutOffInDays").GetInt32());
    }

    [Fact]
    public async Task GivenTheScrumBacklogView_WhenTheCutoffIsQueried_ThenTheNodeHasNoSuchField()
    {
        var navigation = await Query(NavigationQuery, """{"ids":["ari:cloud:jira-software:c:board/6"]}""");
        var viewId = navigation.GetProperty("data").GetProperty("jira_boardsByIds")[0]
            .GetProperty("defaultNavigationItem").GetProperty("id").GetString()!
            .Replace(":navigation-item/", ":view/", StringComparison.Ordinal);

        var cutoff = await Query(CutoffQuery, $$"""{"id":"{{viewId}}"}""");

        Assert.False(cutoff.GetProperty("data").GetProperty("node").TryGetProperty("completedIssueSearchCutOffInDays", out _));
    }

    [Fact]
    public async Task GivenAMutation_WhenPosted_ThenTheFakeRefusesIt()
    {
        const string body = """{"query":"mutation Remove { x }","variables":{}}""";

        using var response = await _client.PostAsync(
            $"{_baseUrl}{FakeGateway.GraphQlPath}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private async Task<JsonElement> Query(string query, string variables)
    {
        var body = $$"""{"query":"{{query}}","variables":{{variables}}}""";

        using var response = await _client.PostAsync(
            $"{_baseUrl}{FakeGateway.GraphQlPath}",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var text = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
