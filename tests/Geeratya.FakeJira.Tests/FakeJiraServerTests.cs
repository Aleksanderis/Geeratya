using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraServerTests : IDisposable
{
    private readonly HttpClient _client = new();
    private FakeJiraServer? _server;

    [Fact]
    public async Task GivenFixtures_WhenMyselfRequested_ThenRecordedAccountIsServed()
    {
        var baseUrl = StartServer(0, 0, 0);

        var body = await _client.GetStringAsync($"{baseUrl}/rest/api/3/myself");

        Assert.Contains("fake-account-1", body, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("/rest/agile/1.0/board/3/issue", "GTM-")]
    [InlineData("/rest/agile/1.0/board/3/backlog", "GTM-")]
    [InlineData("/rest/agile/1.0/board/3/epic", "GTM-4")]
    [InlineData("/rest/agile/1.0/board/4/epic", "GCM-")]
    [InlineData("/rest/api/3/filter/10001", "Filter for GCM board")]
    public async Task GivenBoardAndFilterRoutes_WhenRequested_ThenRecordedFixtureIsServed(string path, string expected)
    {
        var baseUrl = StartServer(0, 0, 0);

        var body = await _client.GetStringAsync($"{baseUrl}{path}");

        Assert.Contains(expected, body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUnknownFilterId_WhenRequested_ThenRecordedNotAvailableReplyWith404()
    {
        var baseUrl = StartServer(0, 0, 0);

        var response = await _client.GetAsync($"{baseUrl}/rest/api/3/filter/99999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("filter is not available", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUnknownPath_WhenRequested_ThenNotFound()
    {
        var baseUrl = StartServer(0, 0, 0);

        var response = await _client.GetAsync($"{baseUrl}/rest/api/3/nothing");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GivenGeneratedIssues_WhenPagingToTheEnd_ThenEveryIssueIsServedOnce()
    {
        var baseUrl = StartServer(120, 0, 0);
        var keys = new List<string>();
        string? token = null;
        var lastPage = false;

        while (!lastPage)
        {
            var query = token is null ? "maxResults=50" : $"maxResults=50&nextPageToken={token}";
            var body = await _client.GetStringAsync(
                $"{baseUrl}/rest/api/3/search/jql?{query}");
            using var document = JsonDocument.Parse(body);

            foreach (var issue in document.RootElement.GetProperty("issues").EnumerateArray())
            {
                keys.Add(issue.GetProperty("key").GetString()!);
            }

            lastPage = document.RootElement.GetProperty("isLast").GetBoolean();
            token = lastPage ? null : document.RootElement.GetProperty("nextPageToken").GetString();
        }

        Assert.Equal(120, keys.Count);
        Assert.Equal(120, keys.Distinct().Count());
    }

    [Fact]
    public async Task GivenNegativePageToken_WhenSearching_ThenBadRequestInsteadOfHang()
    {
        var baseUrl = StartServer(120, 0, 0);

        var response = await _client.GetAsync($"{baseUrl}/rest/api/3/search/jql?maxResults=2&nextPageToken=-5");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GivenRecordedFixtures_WhenPagingByToken_ThenThreePagesAreServedInOrder()
    {
        var baseUrl = StartServer(0, 0, 0);
        var keys = new List<string>();
        string? token = null;
        var pages = 0;

        while (pages < 10)
        {
            var query = token is null ? string.Empty : $"?nextPageToken={Uri.EscapeDataString(token)}";
            var body = await _client.GetStringAsync($"{baseUrl}/rest/api/3/search/jql{query}");
            using var document = JsonDocument.Parse(body);
            pages++;

            foreach (var issue in document.RootElement.GetProperty("issues").EnumerateArray())
            {
                keys.Add(issue.GetProperty("key").GetString()!);
            }

            if (document.RootElement.GetProperty("isLast").GetBoolean())
            {
                break;
            }

            token = document.RootElement.GetProperty("nextPageToken").GetString();
        }

        Assert.Equal(3, pages);
        Assert.Equal(7, keys.Distinct().Count());
    }

    [Fact]
    public async Task GivenRateLimitEveryThird_WhenFourRequestsSent_ThenThirdIsTooManyWithRetryAfter()
    {
        var baseUrl = StartServer(0, 0, 3);
        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? limited = null;

        for (var index = 0; index < 4; index++)
        {
            var response = await _client.GetAsync($"{baseUrl}/rest/api/3/myself");
            statuses.Add(response.StatusCode);
            limited = index == 2 ? response : limited;
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.OK],
            statuses);
        Assert.Equal("1", limited!.Headers.GetValues("Retry-After").Single());
    }

    [Fact]
    public async Task GivenLatency_WhenRequested_ThenReplyIsDelayed()
    {
        var baseUrl = StartServer(0, 300, 0);
        var started = Stopwatch.StartNew();

        await _client.GetStringAsync($"{baseUrl}/rest/api/3/myself");

        Assert.True(started.ElapsedMilliseconds >= 250, $"Took {started.ElapsedMilliseconds} ms.");
    }

    [Fact]
    public void GivenBadNumber_WhenParsing_ThenErrorIsReported()
    {
        var options = FakeJiraOptionsParser.TryParse([FakeJiraOptionsParser.IssuesFlag, "many"], out var error);

        Assert.Null(options);
        Assert.Contains("--issues", error, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        _server?.Dispose();
        _client.Dispose();
    }

    private string StartServer(int issues, int latency, int rateLimitEvery)
    {
        var fixtures = FakeJiraOptionsParser.TryParse([], out var error);
        Assert.True(fixtures is not null, error);
        _server = new FakeJiraServer(fixtures with
        {
            GeneratedIssues = issues,
            LatencyMilliseconds = latency,
            RateLimitEvery = rateLimitEvery,
        });

        return _server.Start();
    }
}
