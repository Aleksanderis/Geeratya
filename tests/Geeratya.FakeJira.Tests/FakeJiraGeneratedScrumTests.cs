using System.Net;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraGeneratedScrumTests : IDisposable
{
    private const string Agile = "/rest/agile/1.0";
    private const int Issues = 1000;

    private readonly HttpClient _client = new();
    private readonly FakeJiraServer _server;
    private readonly string _baseUrl;

    public FakeJiraGeneratedScrumTests()
    {
        var options = FakeJiraOptionsParser.TryParse(
            [FakeJiraOptionsParser.IssuesFlag, Issues.ToString()],
            out var error);
        Assert.True(options is not null, error);
        _server = new FakeJiraServer(options);
        _baseUrl = _server.Start();
    }

    [Fact]
    public async Task GivenGeneratedIssues_WhenBoardsListed_ThenGeneratedScrumBoardIsAddedAndFiltersApply()
    {
        var scrum = await Json($"{Agile}/board?type=scrum");
        var project = await Json($"{Agile}/board?projectKeyOrId=GEN");
        var other = await Json($"{Agile}/board?projectKeyOrId=GCM");

        Assert.Equal([6, 7], [.. scrum.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.Equal([7], [.. project.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.Equal([4], [.. other.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.Equal(7, (await Json($"{Agile}/board/7/configuration")).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GivenGeneratedBoard_WhenSprintsListed_ThenClosedThenActiveThenFutureWithStateFilterAndPaging()
    {
        var all = await Json($"{Agile}/board/7/sprint");
        var open = await Json($"{Agile}/board/7/sprint?state=active,future");
        var page = await Json($"{Agile}/board/7/sprint?startAt=8&maxResults=1");
        var states = all.GetProperty("values").EnumerateArray()
            .Select(sprint => sprint.GetProperty("state").GetString());
        var bad = await _client.GetAsync($"{_baseUrl}{Agile}/board/7/sprint?state=bogus");

        Assert.Equal(10, all.GetProperty("values").GetArrayLength());
        Assert.Equal([.. Enumerable.Repeat("closed", 8), "active", "future"], states);
        Assert.Equal([5009, 5010], [.. open.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.Equal([5009], [.. page.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.False(page.GetProperty("isLast").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task GivenGeneratedSprint_WhenIssuesRead_ThenEveryIssueCarriesAStoryPointEstimate()
    {
        var page = await Json($"{Agile}/sprint/5009/issue?maxResults=5");
        var estimates = page.GetProperty("issues").EnumerateArray()
            .Select(issue => issue.GetProperty("fields").GetProperty("customfield_10047").GetDouble())
            .ToList();

        Assert.Equal(5, estimates.Count);
        Assert.All(estimates, estimate => Assert.InRange(estimate, 1, 8));
    }

    [Fact]
    public async Task GivenGeneratedBacklogIssue_WhenMovedIntoActiveSprintAndRanked_ThenSprintListsItAtThatPosition()
    {
        var backlogKey = Keys(await Json($"{Agile}/board/7/backlog?maxResults=1000"))[^1];
        var reference = Keys(await Json($"{Agile}/sprint/5009/issue"))[0];

        var move = await Send(HttpMethod.Post, $"{Agile}/sprint/5009/issue", $$"""{"issues":["{{backlogKey}}"]}""");
        var rank = await Send(
            HttpMethod.Put,
            $"{Agile}/issue/rank",
            $$"""{"issues":["{{backlogKey}}"],"rankBeforeIssue":"{{reference}}"}""");
        var sprint = await Json($"{Agile}/sprint/5009/issue?maxResults=100");
        var backlog = Keys(await Json($"{Agile}/board/7/backlog?maxResults=1000"));
        var keys = Keys(sprint);

        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, rank.StatusCode);
        Assert.Equal([backlogKey, reference], keys.Take(2));
        Assert.DoesNotContain(backlogKey, backlog);
        Assert.Equal(5009, SprintIds(sprint.GetProperty("issues")[0])[0]);
    }

    [Fact]
    public async Task GivenGeneratedSprintIssue_WhenMovedToBacklog_ThenItLeavesTheSprintAndHasNoSprintField()
    {
        var key = Keys(await Json($"{Agile}/sprint/5009/issue"))[0];

        var move = await Send(HttpMethod.Post, $"{Agile}/backlog/issue", $$"""{"issues":["{{key}}"]}""");
        var sprint = Keys(await Json($"{Agile}/sprint/5009/issue?maxResults=100"));
        var board = await Json($"{Agile}/board/7/issue?maxResults=1000");
        var entry = board.GetProperty("issues").EnumerateArray()
            .First(issue => issue.GetProperty("key").GetString() == key);

        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);
        Assert.DoesNotContain(key, sprint);
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fields").GetProperty("customfield_10020").ValueKind);
    }

    [Fact]
    public async Task GivenGeneratedBoard_WhenMovedIntoClosedSprintOrWithUnknownIssue_ThenBadRequestAndNothingMoves()
    {
        var closed = await Send(HttpMethod.Post, $"{Agile}/sprint/5001/issue", """{"issues":["GEN-801"]}""");
        var unknown = await Send(
            HttpMethod.Post,
            $"{Agile}/sprint/5009/issue",
            """{"issues":["GEN-801","GEN-99999"]}""");
        var malformed = await Send(HttpMethod.Post, $"{Agile}/backlog/issue", "{");
        var sprint = Keys(await Json($"{Agile}/sprint/5009/issue?maxResults=100"));

        Assert.Equal(HttpStatusCode.BadRequest, closed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.DoesNotContain("GEN-801", sprint);
    }

    [Fact]
    public async Task GivenNoActiveSprintFlag_WhenSprintsListed_ThenNoSprintIsActive()
    {
        var options = FakeJiraOptionsParser.TryParse(
            [FakeJiraOptionsParser.IssuesFlag, Issues.ToString(), FakeJiraOptionsParser.NoActiveSprintFlag],
            out var error);
        Assert.True(options is not null, error);
        using var server = new FakeJiraServer(options);
        var baseUrl = server.Start();
        using var stream = await _client.GetStreamAsync($"{baseUrl}{Agile}/board/7/sprint");
        using var document = await JsonDocument.ParseAsync(stream);
        var states = document.RootElement.GetProperty("values").EnumerateArray()
            .Select(sprint => sprint.GetProperty("state").GetString());

        Assert.Equal([.. Enumerable.Repeat("closed", 9), "future"], states);
    }

    [Fact]
    public async Task GivenGeneratedSprint_WhenRead_ThenSprintAndItsPagedIssuesAreServed()
    {
        var sprint = await Json($"{Agile}/sprint/5009");
        var first = await Json($"{Agile}/sprint/5009/issue");
        var second = await Json($"{Agile}/sprint/5009/issue?startAt=50");

        Assert.Equal("active", sprint.GetProperty("state").GetString());
        Assert.Equal(80, first.GetProperty("total").GetInt32());
        Assert.Equal(50, first.GetProperty("issues").GetArrayLength());
        Assert.Equal("GEN-641", Keys(first)[0]);
        Assert.Equal(30, second.GetProperty("issues").GetArrayLength());
        Assert.Equal(5009, SprintIds(first.GetProperty("issues")[0])[0]);
    }

    [Fact]
    public async Task GivenGeneratedBoard_WhenBacklogRead_ThenUnsprintedAndUnfinishedClosedSprintIssuesComeBack()
    {
        var backlog = await Json($"{Agile}/board/7/backlog?maxResults=1000");
        var keys = Keys(backlog);

        Assert.Equal(630, backlog.GetProperty("total").GetInt32());
        Assert.Contains("GEN-1", keys);
        Assert.DoesNotContain("GEN-3", keys);
        Assert.DoesNotContain("GEN-641", keys);
        Assert.Contains("GEN-801", keys);
    }

    [Fact]
    public async Task GivenGeneratedBoard_WhenIssuesRead_ThenJqlNarrowsToOpenOrNamedSprintAndSprintFieldFollows()
    {
        var all = await Json($"{Agile}/board/7/issue");
        var active = await Json($"{Agile}/board/7/issue?jql=sprint%20in%20openSprints()");
        var named = await Json($"{Agile}/board/7/issue?jql=sprint%20%3D%205001");
        var last = await Json($"{Agile}/board/7/issue?startAt=900");
        var field = last.GetProperty("issues")[0].GetProperty("fields").GetProperty("customfield_10020");

        Assert.Equal(Issues, all.GetProperty("total").GetInt32());
        Assert.Equal(80, active.GetProperty("total").GetInt32());
        Assert.Equal(80, named.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, field.ValueKind);
    }

    [Fact]
    public async Task GivenGeneratedBoard_WhenTheScrumBacklogJqlIsAsked_ThenOpenFutureAndUnsprintedIssuesComeBackWithoutClosedOnes()
    {
        const string jql = "sprint in openSprints() or sprint in futureSprints() or sprint is EMPTY";
        var page = await Json($"{Agile}/board/7/issue?maxResults=1000&jql={Uri.EscapeDataString(jql)}");
        var keys = Keys(page);

        Assert.DoesNotContain("GEN-1", keys);
        Assert.Contains("GEN-801", keys);
        Assert.True(page.GetProperty("total").GetInt32() > 80);
    }

    [Fact]
    public async Task GivenGeneratedMode_WhenRecordedIssueMovedToBacklog_ThenRecordedBoardServesIt()
    {
        var move = await Send(HttpMethod.Post, $"{Agile}/backlog/issue", """{"issues":["GCS-15"]}""");
        var backlog = Keys(await Json($"{Agile}/board/6/backlog"));

        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);
        Assert.Contains("GCS-15", backlog);
    }

    [Fact]
    public async Task GivenGeneratedMode_WhenRecordedOrUnknownScrumRoutesAsked_ThenBehaviourIsUnchanged()
    {
        var recorded = await Json($"{Agile}/board/6/sprint");
        var unknown = await _client.GetAsync($"{_baseUrl}{Agile}/sprint/5011");

        Assert.Equal(4, recorded.GetProperty("values").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private static int IdOf(JsonElement element) => element.GetProperty("id").GetInt32();

    private static string[] Keys(JsonElement list)
    {
        return [.. list.GetProperty("issues").EnumerateArray().Select(issue => issue.GetProperty("key").GetString()!)];
    }

    private static int[] SprintIds(JsonElement issue)
    {
        return [.. issue.GetProperty("fields").GetProperty("customfield_10020").EnumerateArray().Select(IdOf)];
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string path, string body)
    {
        var request = new HttpRequestMessage(method, $"{_baseUrl}{path}")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

        return _client.SendAsync(request);
    }

    private async Task<JsonElement> Json(string path)
    {
        using var document = JsonDocument.Parse(await _client.GetStringAsync($"{_baseUrl}{path}"));

        return document.RootElement.Clone();
    }
}
