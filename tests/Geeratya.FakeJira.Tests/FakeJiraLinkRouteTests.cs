using System.Net;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraLinkRouteTests : IDisposable
{
    private readonly HttpClient _client = new();
    private FakeJiraServer? _server;
    private string _baseUrl = string.Empty;

    [Fact]
    public async Task GivenTheFake_WhenLinkTypesAreRequested_ThenTheRecordedTypesAreServed()
    {
        Start();

        var types = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issueLinkType"));

        Assert.Equal(5, types.GetProperty("issueLinkTypes").GetArrayLength());
        Assert.Contains("is blocked by", types.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("GCM-10", "GCM-2")]
    [InlineData("GTM-11", "GTM-9")]
    public async Task GivenTwoRecordedIssues_WhenLinked_ThenEachListsTheOtherOnItsOwnSide(string outward, string inward)
    {
        Start();

        var created = await Send(HttpMethod.Post, "/rest/api/3/issueLink", Body("10000", outward, inward));
        var outwardLinks = await LinksOf(outward);
        var inwardLinks = await LinksOf(inward);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var onOutward = outwardLinks.Single(link => link.GetProperty("id").GetString() == "40000");
        var onInward = inwardLinks.Single(link => link.GetProperty("id").GetString() == "40000");
        Assert.Equal(inward, onOutward.GetProperty("outwardIssue").GetProperty("key").GetString());
        Assert.Equal(outward, onInward.GetProperty("inwardIssue").GetProperty("key").GetString());
        Assert.Equal("blocks", onOutward.GetProperty("type").GetProperty("outward").GetString());
        Assert.Equal("is blocked by", onInward.GetProperty("type").GetProperty("inward").GetString());
    }

    [Fact]
    public async Task GivenALinkWithAnUnknownIssueOrType_WhenPosted_ThenItIsRefusedAndNothingIsListed()
    {
        Start();

        var missing = await Send(HttpMethod.Post, "/rest/api/3/issueLink", Body("10000", "GCM-10", "GCM-9999"));
        var badType = await Send(HttpMethod.Post, "/rest/api/3/issueLink", Body("77", "GCM-10", "GCM-2"));
        var same = await Send(HttpMethod.Post, "/rest/api/3/issueLink", Body("10000", "GCM-10", "GCM-10"));

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badType.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, same.StatusCode);
        Assert.DoesNotContain(await LinksOf("GCM-2"), link => link.GetProperty("id").GetString() == "40000");
    }

    [Fact]
    public async Task GivenACreatedLink_WhenDeleted_ThenItIsGoneFromBothIssuesAndAFurtherDeleteIsNotFound()
    {
        Start();
        await Send(HttpMethod.Post, "/rest/api/3/issueLink", Body("10000", "GCM-10", "GCM-2"));

        var deleted = await Send(HttpMethod.Delete, "/rest/api/3/issueLink/40000", null);
        var again = await Send(HttpMethod.Delete, "/rest/api/3/issueLink/40000", null);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.DoesNotContain(await LinksOf("GCM-10"), link => link.GetProperty("id").GetString() == "40000");
        Assert.DoesNotContain(await LinksOf("GCM-2"), link => link.GetProperty("id").GetString() == "40000");
    }

    [Fact]
    public async Task GivenARecordedLink_WhenDeleted_ThenItDisappearsFromTheIssueReadAndTheSearch()
    {
        Start();
        Assert.Contains(await LinksOf("GTM-11"), link => link.GetProperty("id").GetString() == "10004");

        var deleted = await Send(HttpMethod.Delete, "/rest/api/3/issueLink/10004", null);
        var search = await _client.GetStringAsync(
            $"{_baseUrl}/rest/api/3/search/jql?jql={Uri.EscapeDataString("key = GTM-11")}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.DoesNotContain(await LinksOf("GTM-11"), link => link.GetProperty("id").GetString() == "10004");
        var searched = Parse(search).GetProperty("issues")[0].GetProperty("fields").GetProperty("issuelinks");
        Assert.DoesNotContain(searched.EnumerateArray(), link => link.GetProperty("id").GetString() == "10004");
        Assert.Contains(await LinksOf("GTM-11"), link => link.GetProperty("id").GetString() == "10005");
    }

    [Fact]
    public async Task GivenACreatedIssue_WhenLinked_ThenItsReadListsTheLink()
    {
        Start();
        var created = await Send(
            HttpMethod.Post,
            "/rest/api/3/issue",
            """{"fields":{"project":{"key":"GTM"},"issuetype":{"name":"Task"},"summary":"New one"}}""");
        var key = Parse(await created.Content.ReadAsStringAsync()).GetProperty("key").GetString()!;

        await Send(HttpMethod.Post, "/rest/api/3/issueLink", Body("10003", key, "GTM-11"));
        var links = await LinksOf(key);

        var link = Assert.Single(links);
        Assert.Equal("GTM-11", link.GetProperty("outwardIssue").GetProperty("key").GetString());
    }

    public void Dispose()
    {
        _server?.Dispose();
        _client.Dispose();
    }

    private static string Body(string typeId, string outward, string inward)
    {
        var type = $"{{\"id\":\"{typeId}\"}}";
        var inwardIssue = $"{{\"key\":\"{inward}\"}}";
        var outwardIssue = $"{{\"key\":\"{outward}\"}}";

        return $"{{\"type\":{type},\"inwardIssue\":{inwardIssue},\"outwardIssue\":{outwardIssue}}}";
    }

    private static JsonElement Parse(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private async Task<List<JsonElement>> LinksOf(string key)
    {
        var issue = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/{key}"));

        if (!issue.GetProperty("fields").TryGetProperty("issuelinks", out var links))
        {
            return [];
        }

        return [.. links.EnumerateArray()];
    }

    private void Start()
    {
        var options = FakeJiraOptionsParser.TryParse([], out var error);
        Assert.True(options is not null, error);
        _server = new FakeJiraServer(options);
        _baseUrl = _server.Start();
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string? body)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return _client.SendAsync(request);
    }
}
