using System.Net;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraScrumRouteTests : IDisposable
{
    private const string Agile = "/rest/agile/1.0";
    private const string StartBody =
        """{"state":"active","startDate":"2026-11-02T10:00:00.000Z","endDate":"2026-11-09T10:00:00.000Z"}""";

    private readonly HttpClient _client = new();
    private readonly FakeJiraServer _server;
    private readonly string _baseUrl;

    public FakeJiraScrumRouteTests()
    {
        var options = FakeJiraOptionsParser.TryParse([], out var error);
        Assert.True(options is not null, error);
        _server = new FakeJiraServer(options);
        _baseUrl = _server.Start();
    }

    [Fact]
    public async Task GivenBoardList_WhenRead_ThenAllFourBoardsComeBackAndTypeNarrowsToScrum()
    {
        var all = await Json($"{Agile}/board");
        var scrum = await Json($"{Agile}/board?type=scrum");

        Assert.Equal([3, 4, 5, 6], [.. all.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.Equal([6], [.. scrum.GetProperty("values").EnumerateArray().Select(IdOf)]);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    public async Task GivenScrumBoard_WhenOpened_ThenBoardConfigurationAndEpicsAreServed(int board)
    {
        var detail = await Json($"{Agile}/board/{board}");
        var configuration = await Json($"{Agile}/board/{board}/configuration");
        var epics = await Json($"{Agile}/board/{board}/epic");

        Assert.Equal(board, detail.GetProperty("id").GetInt32());
        Assert.Equal("field", configuration.GetProperty("estimation").GetProperty("type").GetString());
        Assert.True(epics.GetProperty("values").GetArrayLength() > 0);
    }

    [Fact]
    public async Task GivenCompanyManagedBoard_WhenSprintsListed_ThenClosedActiveFutureOrderAndStateFilterApply()
    {
        var all = await Json($"{Agile}/board/6/sprint");
        var open = await Json($"{Agile}/board/6/sprint?state=active,future");
        var bad = await _client.GetAsync($"{_baseUrl}{Agile}/board/6/sprint?state=bogus");

        Assert.Equal([6, 7, 2, 8], [.. all.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.Equal([7, 2, 8], [.. open.GetProperty("values").EnumerateArray().Select(IdOf)]);
        Assert.True(all.GetProperty("isLast").GetBoolean());
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("Illegal value bogus", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenTeamManagedBoard_WhenSprintsPaged_ThenOffsetAndIsLastFollow()
    {
        var first = await Json($"{Agile}/board/5/sprint?maxResults=3");
        var second = await Json($"{Agile}/board/5/sprint?maxResults=3&startAt=3");

        Assert.Equal(3, first.GetProperty("values").GetArrayLength());
        Assert.False(first.GetProperty("isLast").GetBoolean());
        Assert.Equal(1, second.GetProperty("values").GetArrayLength());
        Assert.True(second.GetProperty("isLast").GetBoolean());
        Assert.Equal(4, second.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task GivenKanbanBoard_WhenSprintsListed_ThenRecordedDoesNotSupportSprints(int board)
    {
        var response = await _client.GetAsync($"{_baseUrl}{Agile}/board/{board}/sprint");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var text = await response.Content.ReadAsStringAsync();

        Assert.Contains("does not support sprints", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUnknownBoardOrSprint_WhenRead_ThenNotFound()
    {
        var board = await _client.GetAsync($"{_baseUrl}{Agile}/board/99/sprint");
        var sprint = await _client.GetAsync($"{_baseUrl}{Agile}/sprint/99");
        var issues = await _client.GetAsync($"{_baseUrl}{Agile}/sprint/99/issue");

        Assert.Equal(HttpStatusCode.NotFound, board.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, sprint.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, issues.StatusCode);
    }

    [Fact]
    public async Task GivenSprintIssues_WhenRead_ThenMembersPagedWithoutIsLast()
    {
        var active = await Json($"{Agile}/sprint/7/issue");
        var page = await Json($"{Agile}/sprint/7/issue?maxResults=3&startAt=6");

        Assert.Equal(7, active.GetProperty("total").GetInt32());
        Assert.Equal(["GCS-8", "GCS-9", "GCS-10", "GCS-14", "GCS-11", "GCS-12", "GCS-13"], Keys(active));
        Assert.Equal(["GCS-13"], Keys(page));
        Assert.False(page.TryGetProperty("isLast", out _));
    }

    [Fact]
    public async Task GivenTeamManagedActiveSprint_WhenRead_ThenIssueListedOnlyInTheBacklogIsMissing()
    {
        var active = await Json($"{Agile}/sprint/10/issue");
        var backlog = await Json($"{Agile}/board/5/backlog");

        Assert.DoesNotContain("GTS-35", Keys(active));
        Assert.Contains("GTS-35", Keys(backlog));
        Assert.Equal(["GTS-22", "GTS-23", "GTS-35", "GTS-39", "GTS-40", "GTS-41", "GTS-42"], Keys(backlog));
    }

    [Fact]
    public async Task GivenCompanyManagedBoard_WhenBacklogRead_ThenFutureSprintIssuesAreNotInIt()
    {
        var backlog = await Json($"{Agile}/board/6/backlog");
        var board = await Json($"{Agile}/board/6/issue");
        var openOnly = await Json($"{Agile}/board/6/issue?jql={Uri.EscapeDataString("sprint in openSprints()")}");

        Assert.Equal(["GCS-18", "GCS-19", "GCS-20", "GCS-21"], Keys(backlog));
        Assert.Equal(19, board.GetProperty("total").GetInt32());
        Assert.Equal(7, openOnly.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GivenCarriedOverIssue_WhenRead_ThenSprintFieldListsClosedThenActive()
    {
        var active = await Json($"{Agile}/sprint/7/issue");
        var carried = active.GetProperty("issues").EnumerateArray().First(issue => Key(issue) == "GCS-8");
        var sprints = carried.GetProperty("fields").GetProperty("customfield_10020").EnumerateArray();

        Assert.Equal([6, 7], [.. sprints.Select(IdOf)]);
    }

    [Fact]
    public async Task GivenIssueInActiveSprint_WhenMovedToFutureSprint_ThenItLeavesTheFirstAndFieldFollows()
    {
        var move = await Send(HttpMethod.Post, $"{Agile}/sprint/8/issue", """{"issues":["GCS-10"]}""");
        var active = await Json($"{Agile}/sprint/7/issue");
        var future = await Json($"{Agile}/sprint/8/issue");
        var moved = future.GetProperty("issues").EnumerateArray().First(issue => Key(issue) == "GCS-10");
        var sprints = moved.GetProperty("fields").GetProperty("customfield_10020").EnumerateArray();

        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);
        Assert.DoesNotContain("GCS-10", Keys(active));
        Assert.Contains("GCS-10", Keys(future));
        Assert.Equal([8], [.. sprints.Select(IdOf)]);
    }

    [Fact]
    public async Task GivenIssueInSprint_WhenMovedToBacklog_ThenItIsInTheBacklogAndHasNoSprint()
    {
        var move = await Send(HttpMethod.Post, $"{Agile}/backlog/issue", """{"issues":["GCS-15"]}""");
        var future = await Json($"{Agile}/sprint/8/issue");
        var backlog = await Json($"{Agile}/board/6/backlog");
        var entry = backlog.GetProperty("issues").EnumerateArray().First(issue => Key(issue) == "GCS-15");

        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);
        Assert.DoesNotContain("GCS-15", Keys(future));
        Assert.Equal(JsonValueKind.Null, entry.GetProperty("fields").GetProperty("customfield_10020").ValueKind);
    }

    [Theory]
    [InlineData("GCS-3")]
    [InlineData("GTS-24")]
    public async Task GivenScrumBoardIssue_WhenDetailAndKeySearchRead_ThenBothServeItWithCommentsAndLinks(string key)
    {
        var detail = await Json($"/rest/api/3/issue/{key}");
        var search = await Json($"/rest/api/3/search/jql?jql={Uri.EscapeDataString($"key = {key}")}");
        var comments = await Json($"/rest/api/3/issue/{key}/comment");
        var links = await _client.GetAsync($"{_baseUrl}/rest/api/3/issue/{key}/remotelink");

        Assert.Equal(key, Key(detail));
        Assert.Equal([key], [.. search.GetProperty("issues").EnumerateArray().Select(Key)]);
        Assert.Equal(0, comments.GetProperty("comments").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, links.StatusCode);
    }

    [Fact]
    public async Task GivenScrumIssueMoved_WhenDetailRead_ThenSprintFieldFollowsMembership()
    {
        await Send(HttpMethod.Post, $"{Agile}/sprint/8/issue", """{"issues":["GCS-10"]}""");

        var detail = await Json("/rest/api/3/issue/GCS-10");
        var sprints = detail.GetProperty("fields").GetProperty("customfield_10020").EnumerateArray();

        Assert.Equal([8], [.. sprints.Select(IdOf)]);
    }

    [Fact]
    public async Task GivenUnknownScrumKey_WhenDetailRead_ThenNotFound()
    {
        var response = await _client.GetAsync($"{_baseUrl}/rest/api/3/issue/GCS-9999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GivenClosedSprint_WhenIssuesMovedIn_ThenRecordedBadRequestAndNothingMoves()
    {
        var move = await Send(HttpMethod.Post, $"{Agile}/sprint/6/issue", """{"issues":["GCS-18"]}""");
        var backlog = await Json($"{Agile}/board/6/backlog");

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.Contains("has not been completed", await move.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Contains("GCS-18", Keys(backlog));
    }

    [Fact]
    public async Task GivenUnknownIssueAmongKeys_WhenMoved_ThenBadRequestAndNoneMoves()
    {
        var move = await Send(HttpMethod.Post, $"{Agile}/sprint/8/issue", """{"issues":["GCS-18","GCS-999"]}""");
        var future = await Json($"{Agile}/sprint/8/issue");

        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
        Assert.DoesNotContain("GCS-18", Keys(future));
    }

    [Fact]
    public async Task GivenNewSprint_WhenCreated_ThenItIsFutureListedAndFirstSprintOfNoIssues()
    {
        var created = await Send(
            HttpMethod.Post,
            $"{Agile}/sprint",
            """{"name":"Next","originBoardId":5,"goal":"ship"}""");
        var sprint = Parse(await created.Content.ReadAsStringAsync());
        var listed = await Json($"{Agile}/board/5/sprint?state=future");
        var issues = await Json($"{Agile}/sprint/{IdOf(sprint)}/issue");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("future", sprint.GetProperty("state").GetString());
        Assert.Equal("ship", sprint.GetProperty("goal").GetString());
        Assert.Contains(IdOf(sprint), listed.GetProperty("values").EnumerateArray().Select(IdOf));
        Assert.Equal(0, issues.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("""{"originBoardId":5}""")]
    [InlineData("""{"name":"x","originBoardId":4}""")]
    [InlineData("not json")]
    public async Task GivenInvalidSprint_WhenCreated_ThenBadRequest(string body)
    {
        var created = await Send(HttpMethod.Post, $"{Agile}/sprint", body);

        Assert.Equal(HttpStatusCode.BadRequest, created.StatusCode);
    }

    [Fact]
    public async Task GivenFutureSprint_WhenStartedWithoutDatesThenWithThem_ThenOnlyTheSecondActivates()
    {
        var without = await Send(HttpMethod.Put, $"{Agile}/sprint/8", """{"name":"Seed Sprint 3","state":"active"}""");
        var with = await Send(HttpMethod.Put, $"{Agile}/sprint/8", StartBody);
        var started = Parse(await with.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.BadRequest, without.StatusCode);
        Assert.Contains("start date", await without.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, with.StatusCode);
        Assert.Equal("active", started.GetProperty("state").GetString());
    }

    [Fact]
    public async Task GivenActiveSprint_WhenCompleted_ThenClosedWithCompleteDateAndUnfinishedIssuesReturnToBacklog()
    {
        var before = await Json($"{Agile}/sprint/7/issue");
        var unfinished = before.GetProperty("issues").EnumerateArray().Where(NotDone).Select(Key).ToArray();

        var complete = await Send(HttpMethod.Put, $"{Agile}/sprint/7", """{"state":"closed"}""");
        var closed = Parse(await complete.Content.ReadAsStringAsync());
        var backlog = Keys(await Json($"{Agile}/board/6/backlog"));

        Assert.NotEmpty(unfinished);
        Assert.Equal(HttpStatusCode.OK, complete.StatusCode);
        Assert.Equal("closed", closed.GetProperty("state").GetString());
        Assert.True(closed.TryGetProperty("completeDate", out _));
        Assert.All(unfinished, key => Assert.Contains(key, backlog));
        Assert.Equal(unfinished.Length + 4, backlog.Length);
    }

    [Fact]
    public async Task GivenComplete_WhenMoveIssuesToSent_ThenBadRequestAndSprintStaysActive()
    {
        var complete = await Send(HttpMethod.Put, $"{Agile}/sprint/7", """{"state":"closed","moveIssuesTo":8}""");
        var sprint = await Json($"{Agile}/sprint/7");

        Assert.Equal(HttpStatusCode.BadRequest, complete.StatusCode);
        var text = await complete.Content.ReadAsStringAsync();

        Assert.Contains("Invalid request payload", text, StringComparison.Ordinal);
        Assert.Equal("active", sprint.GetProperty("state").GetString());
    }

    [Fact]
    public async Task GivenCompleteThenSecondCall_WhenUnfinishedIssuesMoved_ThenTheyLeaveTheBacklogForTheNextSprint()
    {
        await Send(HttpMethod.Put, $"{Agile}/sprint/7", """{"state":"closed"}""");
        var backlog = Keys(await Json($"{Agile}/board/6/backlog"));
        var move = await Send(HttpMethod.Post, $"{Agile}/sprint/8/issue", $$"""{"issues":["{{backlog[0]}}"]}""");
        var after = Keys(await Json($"{Agile}/board/6/backlog"));

        Assert.Equal(HttpStatusCode.NoContent, move.StatusCode);
        Assert.DoesNotContain(backlog[0], after);
        Assert.Contains(backlog[0], Keys(await Json($"{Agile}/sprint/8/issue")));
    }

    [Fact]
    public async Task GivenUnknownStateOrForeignBoardIssue_WhenSent_ThenBadRequestAndNothingChanges()
    {
        var state = await Send(HttpMethod.Put, $"{Agile}/sprint/8", """{"state":"bogus"}""");
        var foreign = await Send(HttpMethod.Post, $"{Agile}/sprint/8/issue", """{"issues":["GTS-22"]}""");
        var sprint = await Json($"{Agile}/sprint/8");

        Assert.Equal(HttpStatusCode.BadRequest, state.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, foreign.StatusCode);
        Assert.Equal("future", sprint.GetProperty("state").GetString());
    }

    [Fact]
    public async Task GivenBadTransitions_WhenSent_ThenFutureToClosedAndReopenAreRecordedBadRequests()
    {
        var futureToClosed = await Send(HttpMethod.Put, $"{Agile}/sprint/8", """{"state":"closed"}""");
        var reopen = await Send(HttpMethod.Put, $"{Agile}/sprint/6", """{"state":"active"}""");
        var again = await Send(HttpMethod.Put, $"{Agile}/sprint/6", """{"state":"closed"}""");

        Assert.Equal(HttpStatusCode.BadRequest, futureToClosed.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, reopen.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
    }

    [Fact]
    public async Task GivenSprintWithGoal_WhenReplacedAndPartiallyUpdated_ThenOnlyReplaceDropsTheGoal()
    {
        var created = await Send(HttpMethod.Post, $"{Agile}/sprint", """{"name":"A","originBoardId":6,"goal":"g"}""");
        var id = IdOf(Parse(await created.Content.ReadAsStringAsync()));

        var partial = await Send(HttpMethod.Post, $"{Agile}/sprint/{id}", """{"name":"B"}""");
        var replaced = await Send(HttpMethod.Put, $"{Agile}/sprint/{id}", """{"name":"C"}""");

        Assert.Equal("g", Parse(await partial.Content.ReadAsStringAsync()).GetProperty("goal").GetString());
        Assert.False(Parse(await replaced.Content.ReadAsStringAsync()).TryGetProperty("goal", out _));
    }

    [Fact]
    public async Task GivenSprintWithIssues_WhenDeleted_ThenGoneAndIssuesHaveNoSprint()
    {
        var deleted = await Send(HttpMethod.Delete, $"{Agile}/sprint/8", null);
        var gone = await _client.GetAsync($"{_baseUrl}{Agile}/sprint/8");
        var backlog = await Json($"{Agile}/board/6/backlog");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Contains("GCS-15", Keys(backlog));
    }

    public void Dispose()
    {
        _server.Dispose();
        _client.Dispose();
    }

    private static JsonElement Parse(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private static int IdOf(JsonElement element) => element.GetProperty("id").GetInt32();

    private static string Key(JsonElement issue) => issue.GetProperty("key").GetString()!;

    private static string[] Keys(JsonElement list) => [.. list.GetProperty("issues").EnumerateArray().Select(Key)];

    private static bool NotDone(JsonElement issue)
    {
        var category = issue.GetProperty("fields").GetProperty("status").GetProperty("statusCategory");

        return category.GetProperty("key").GetString() != "done";
    }

    private async Task<JsonElement> Json(string path) => Parse(await _client.GetStringAsync($"{_baseUrl}{path}"));

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
