using System.Net;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraDemoTests : IDisposable
{
    private const string Agile = "/rest/agile/1.0";
    private const string Api = "/rest/api/3";

    private readonly HttpClient _client = new();
    private readonly FakeJiraServer _server;
    private readonly string _baseUrl;

    public FakeJiraDemoTests()
    {
        var options = FakeJiraOptionsParser.TryParse([FakeJiraOptionsParser.DemoFlag], out var error);
        Assert.True(options is not null, error);
        _server = new FakeJiraServer(options);
        _baseUrl = _server.Start();
    }

    [Fact]
    public async Task GivenDemoMode_WhenBoardListed_ThenOnlyTheWayfinderScrumBoardAnswers()
    {
        var boards = await Json($"{Agile}/board?projectKeyOrId=WAY&type=scrum");
        var other = await Json($"{Agile}/board?projectKeyOrId=GCM");

        Assert.Equal(
            ["Wayfinder board"],
            [.. boards.GetProperty("values").EnumerateArray().Select(board => board.GetProperty("name").GetString()!)]);
        Assert.Equal(0, other.GetProperty("values").GetArrayLength());
    }

    [Fact]
    public async Task GivenDemoMode_WhenActiveSprintAndBacklogRead_ThenThePlannedMixOfStatusesAndEpicsComesBack()
    {
        var active = await Json($"{Agile}/board/20/issue?jql=sprint in openSprints()&maxResults=100");
        var backlog = await Json($"{Agile}/board/20/backlog");

        var statuses = active.GetProperty("issues").EnumerateArray()
            .Select(issue => issue.GetProperty("fields").GetProperty("status").GetProperty("name").GetString()!)
            .Distinct();

        Assert.Equal(14, active.GetProperty("total").GetInt32());
        Assert.Equal(["Done", "In Progress", "In Review", "To Do"], statuses.Order());
        Assert.Equal(8, backlog.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task GivenJqlWithAndOrAndOrderBy_WhenSearched_ThenResultsFollowTheQueryAndPriorityDescIsHighestFirst()
    {
        var mine = await Search("assignee = currentUser() AND statusCategory != Done ORDER BY priority DESC");
        var bugs = await Search("project = WAY AND (type = Bug OR labels = crash) AND status != Done");

        Assert.Equal(["WAY-15", "WAY-4", "WAY-18", "WAY-24"], mine);
        Assert.Contains("WAY-16", bugs);
        Assert.DoesNotContain("WAY-9", bugs);
    }

    [Fact]
    public async Task GivenTransition_WhenPostedAndIssueRead_ThenTheNewStatusIsShownAndBadIdIsRejected()
    {
        var done = await _client.PostAsync(
            $"{_baseUrl}{Api}/issue/WAY-13/transitions",
            new StringContent("""{"transition":{"id":"41"}}""", Encoding.UTF8, "application/json"));
        var bad = await _client.PostAsync(
            $"{_baseUrl}{Api}/issue/WAY-13/transitions",
            new StringContent("""{"transition":{"id":"99"}}""", Encoding.UTF8, "application/json"));
        var issue = await Json($"{Api}/issue/WAY-13");

        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Equal("Done", issue.GetProperty("fields").GetProperty("status").GetProperty("name").GetString());
    }

    [Fact]
    public async Task GivenIssueWithStackTraceComment_WhenCommentsRead_ThenNewestFirstAndCodeBlockSurvives()
    {
        var comments = await Json($"{Api}/issue/WAY-16/comment?orderBy=-created");
        var oldest = comments.GetProperty("comments")[2].GetProperty("body").GetRawText();

        Assert.Equal(3, comments.GetProperty("total").GetInt32());
        Assert.Contains("\"codeBlock\"", oldest, StringComparison.Ordinal);
        Assert.Contains("NullPointerException", oldest, StringComparison.Ordinal);
    }

    [Fact]
    public void GivenBareFence_WhenDocumentBuilt_ThenCodeBlockHasNoLanguage()
    {
        var document = DemoAdf.Document("```\nplain\n```");

        var block = document["content"]![0]!;

        Assert.Equal("codeBlock", block["type"]!.GetValue<string>());
        Assert.Null(block["attrs"]);
        Assert.Equal("plain", block["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void GivenFenceWithLanguage_WhenDocumentBuilt_ThenCodeBlockCarriesTheLanguage()
    {
        var document = DemoAdf.Document("```kotlin\nval x = 1\n```");

        var block = document["content"]![0]!;

        Assert.Equal("kotlin", block["attrs"]!["language"]!.GetValue<string>());
    }

    [Fact]
    public async Task GivenJqlOperators_WhenSearched_ThenInIsEmptyAndTextMatchOnPlainTextOnly()
    {
        var unassigned = await Search("project = WAY AND assignee is EMPTY AND sprint = 16");
        var listed = await Search("key in (WAY-12, WAY-15) ORDER BY key DESC");
        var text = await Search("text ~ \"stack trace\"");
        var nodeName = await Search("description ~ paragraph");
        var future = await Search("sprint in futureSprints()");

        Assert.Equal(["WAY-26", "WAY-27", "WAY-28"], unassigned);
        Assert.Equal(["WAY-15", "WAY-12"], listed);
        Assert.Equal(["WAY-16"], text);
        Assert.Empty(nodeName);
        Assert.Equal(["WAY-26", "WAY-27", "WAY-28"], future);
    }

    [Fact]
    public async Task GivenEditAndSprintMoveAndRank_WhenPosted_ThenLaterReadsShowThem()
    {
        await Send(HttpMethod.Put, $"{Api}/issue/WAY-30", """{"fields":{"summary":"Offline search ignores accents","priority":{"name":"High"}}}""");
        await Send(HttpMethod.Post, $"{Agile}/sprint/16/issue", """{"issues":["WAY-30"]}""");
        await Send(HttpMethod.Put, $"{Agile}/issue/rank", """{"issues":["WAY-36"],"rankBeforeIssue":"WAY-29"}""");

        var issue = await Json($"{Api}/issue/WAY-30");
        var future = await Json($"{Agile}/sprint/16/issue");
        var backlog = await Json($"{Agile}/board/20/backlog");

        Assert.Equal("Offline search ignores accents", issue.GetProperty("fields").GetProperty("summary").GetString());
        Assert.Equal("High", issue.GetProperty("fields").GetProperty("priority").GetProperty("name").GetString());
        Assert.Equal(4, future.GetProperty("total").GetInt32());
        Assert.Equal("WAY-36", backlog.GetProperty("issues")[0].GetProperty("key").GetString());
    }

    [Fact]
    public async Task GivenEmptyBodiesAndClosedSprint_WhenPosted_ThenBadRequestInsteadOfServerError()
    {
        var transition = await Send(HttpMethod.Post, $"{Api}/issue/WAY-14/transitions", string.Empty);
        var closed = await Send(HttpMethod.Post, $"{Agile}/sprint/14/issue", """{"issues":["WAY-14"]}""");

        Assert.Equal(HttpStatusCode.BadRequest, transition);
        Assert.Equal(HttpStatusCode.BadRequest, closed);
    }

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
    }

    private async Task<HttpStatusCode> Send(HttpMethod method, string path, string body)
    {
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(method, $"{_baseUrl}{path}");
        request.Content = content;
        using var response = await _client.SendAsync(request);

        return response.StatusCode;
    }

    private async Task<List<string>> Search(string jql)
    {
        var page = await Json($"{Api}/search/jql?jql={Uri.EscapeDataString(jql)}");

        return [.. page.GetProperty("issues").EnumerateArray().Select(issue => issue.GetProperty("key").GetString()!)];
    }

    private async Task<JsonElement> Json(string path)
    {
        var text = await _client.GetStringAsync($"{_baseUrl}{path.Replace(" ", "%20", StringComparison.Ordinal)}");

        return JsonDocument.Parse(text).RootElement.Clone();
    }
}
