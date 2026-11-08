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

    public void Dispose()
    {
        _client.Dispose();
        _server.Dispose();
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
