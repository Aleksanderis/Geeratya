using System.Net;
using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira.Tests;

public sealed class FakeJiraWriteRouteTests : IDisposable
{
    private const string CreateBody =
        """{"fields":{"project":{"key":"GTM"},"issuetype":{"name":"Task"},"summary":"New one","labels":["geeratya-test-1"]}}""";

    private const string CommentBody =
        """{"body":{"type":"doc","version":1,"content":[{"type":"paragraph","content":[{"type":"text","text":"hello"}]}]}}""";

    private readonly HttpClient _client = new();
    private FakeJiraServer? _server;
    private string _baseUrl = string.Empty;

    [Fact]
    public async Task GivenCreateBody_WhenPosted_ThenIssueIsCreatedAndReadableWithItsFields()
    {
        Start(0);

        var created = await Send(HttpMethod.Post, "/rest/api/3/issue", CreateBody);
        var key = Parse(await created.Content.ReadAsStringAsync()).GetProperty("key").GetString();
        var issue = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/{key}"));

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.StartsWith("GTM-", key, StringComparison.Ordinal);
        Assert.Equal("New one", issue.GetProperty("fields").GetProperty("summary").GetString());
        Assert.Equal("To Do", issue.GetProperty("fields").GetProperty("status").GetProperty("name").GetString());
    }

    [Fact]
    public async Task GivenCreateWithoutSummary_WhenPosted_ThenRecordedValidationErrorAndNothingCreated()
    {
        Start(0);

        var response = await Send(
            HttpMethod.Post,
            "/rest/api/3/issue",
            """{"fields":{"project":{"key":"GTM"},"issuetype":{"name":"Task"}}}""");
        var text = await response.Content.ReadAsStringAsync();
        var next = await Send(HttpMethod.Post, "/rest/api/3/issue", CreateBody);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Summary is required.", text, StringComparison.Ordinal);
        Assert.Contains("GTM-100", await next.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenRecordedIssue_WhenSummaryEdited_ThenReadShowsItAndSearchByKeyToo()
    {
        Start(0);

        var edit = await Send(HttpMethod.Put, "/rest/api/3/issue/GTM-11", """{"fields":{"summary":"Renamed"}}""");
        var issue = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GTM-11"));
        var search = await _client.GetStringAsync(
            $"{_baseUrl}/rest/api/3/search/jql?jql={Uri.EscapeDataString("key = GTM-11")}");

        Assert.Equal(HttpStatusCode.NoContent, edit.StatusCode);
        Assert.Equal("Renamed", issue.GetProperty("fields").GetProperty("summary").GetString());
        Assert.Contains("Renamed", search, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUnknownField_WhenEditing_ThenRecordedValidationErrorAndIssueUnchanged()
    {
        Start(0);

        var edit = await Send(HttpMethod.Put, "/rest/api/3/issue/GTM-11", """{"fields":{"nosuchfield":"x"}}""");
        var text = await edit.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Contains("cannot be set", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenUpdateStyleEdit_WhenPut_ThenUnknownFieldErrorNotSummaryRequired()
    {
        Start(0);

        var edit = await Send(HttpMethod.Put, "/rest/api/3/issue/GTM-11", """{"update":{"labels":[{"add":"x"}]}}""");

        Assert.Equal(HttpStatusCode.BadRequest, edit.StatusCode);
        Assert.Contains("cannot be set", await edit.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GivenNonStringSummary_WhenCreating_ThenBadRequestNotServerError()
    {
        Start(0);

        var response = await Send(
            HttpMethod.Post,
            "/rest/api/3/issue",
            """{"fields":{"project":{"key":"GTM"},"issuetype":{"name":"Task"},"summary":5}}""");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GivenCreatedAndRecordedIssue_WhenDeleting_ThenCreatedIsGoneAndRecordedIsForbidden()
    {
        Start(0);
        var created = await Send(HttpMethod.Post, "/rest/api/3/issue", CreateBody);
        var key = Parse(await created.Content.ReadAsStringAsync()).GetProperty("key").GetString();

        var deleted = await Send(HttpMethod.Delete, $"/rest/api/3/issue/{key}", null);
        var gone = await _client.GetAsync($"{_baseUrl}/rest/api/3/issue/{key}");
        var recorded = await Send(HttpMethod.Delete, "/rest/api/3/issue/GTM-11", null);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, recorded.StatusCode);
    }

    [Fact]
    public async Task GivenTransition_WhenPosted_ThenStatusChangesAndBadIdIsRecordedError()
    {
        Start(0);
        var list = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GTM-11/transitions"));
        var first = list.GetProperty("transitions")[0];
        var id = first.GetProperty("id").GetString();
        var name = first.GetProperty("to").GetProperty("name").GetString();

        var done = await Send(HttpMethod.Post, "/rest/api/3/issue/GTM-11/transitions", $"{{\"transition\":{{\"id\":\"{id}\"}}}}");
        var bad = await Send(HttpMethod.Post, "/rest/api/3/issue/GTM-11/transitions", """{"transition":{"id":"999"}}""");
        var issue = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GTM-11"));

        Assert.Equal(HttpStatusCode.NoContent, done.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("not valid", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(name, issue.GetProperty("fields").GetProperty("status").GetProperty("name").GetString());
    }

    [Fact]
    public async Task GivenAssign_WhenPutThenCleared_ThenTheIssueReadsWithTheAssigneeThenWithout()
    {
        Start(0);
        var path = "/rest/api/3/issue/GTM-11";

        var assigned = await Send(HttpMethod.Put, $"{path}/assignee", """{"accountId":"fake-account-1"}""");
        var after = Parse(await _client.GetStringAsync($"{_baseUrl}{path}")).GetProperty("fields");
        var cleared = await Send(HttpMethod.Put, $"{path}/assignee", """{"accountId":null}""");
        var without = Parse(await _client.GetStringAsync($"{_baseUrl}{path}")).GetProperty("fields");

        Assert.Equal(HttpStatusCode.NoContent, assigned.StatusCode);
        Assert.Equal("Test User 1", after.GetProperty("assignee").GetProperty("displayName").GetString());
        Assert.Equal("fake-account-1", after.GetProperty("assignee").GetProperty("accountId").GetString());
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        Assert.Equal(JsonValueKind.Null, without.GetProperty("assignee").ValueKind);
    }

    [Fact]
    public async Task GivenAssign_WhenIssueIsUnknownOrBodyIsEmpty_ThenItIsARecordedError()
    {
        Start(0);

        var missing = await Send(HttpMethod.Put, "/rest/api/3/issue/ZZZ-1/assignee", """{"accountId":"x"}""");
        var empty = await Send(HttpMethod.Put, "/rest/api/3/issue/GTM-11/assignee", "{}");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task GivenComment_WhenAddedEditedAndDeleted_ThenListFollowsEachStep()
    {
        Start(0);
        var added = await Send(HttpMethod.Post, "/rest/api/3/issue/GCM-2/comment", CommentBody);
        var id = Parse(await added.Content.ReadAsStringAsync()).GetProperty("id").GetString();
        var afterAdd = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GCM-2/comment"));

        var edited = await Send(
            HttpMethod.Put,
            $"/rest/api/3/issue/GCM-2/comment/{id}",
            CommentBody.Replace("hello", "changed", StringComparison.Ordinal));
        var afterEdit = await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GCM-2/comment");
        var deleted = await Send(HttpMethod.Delete, $"/rest/api/3/issue/GCM-2/comment/{id}", null);
        var afterDelete = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GCM-2/comment"));
        var missing = await Send(HttpMethod.Delete, $"/rest/api/3/issue/GCM-2/comment/{id}", null);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Equal(1, afterAdd.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        Assert.Contains("changed", afterEdit, StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(0, afterDelete.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task GivenRank_WhenIssuesExistOrNot_ThenNoContentOrRecordedError()
    {
        Start(0);

        var ranked = await Send(
            HttpMethod.Put,
            "/rest/agile/1.0/issue/rank",
            """{"issues":["GTM-11"],"rankBeforeIssue":"GTM-9"}""");
        var missing = await Send(
            HttpMethod.Put,
            "/rest/agile/1.0/issue/rank",
            """{"issues":["GTM-11"],"rankBeforeIssue":"GTM-9999"}""");

        Assert.Equal(HttpStatusCode.NoContent, ranked.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task GivenDropWriteEvery_WhenWritingOnAFreshConnection_ThenWriteIsAppliedButNeverAnswered()
    {
        Start(1);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Send(HttpMethod.Post, "/rest/api/3/issue", CreateBody.Replace("New one", "Applied anyway", StringComparison.Ordinal)));
        var issue = Parse(await _client.GetStringAsync($"{_baseUrl}/rest/api/3/issue/GTM-100"));

        Assert.Equal("Applied anyway", issue.GetProperty("fields").GetProperty("summary").GetString());
    }

    public void Dispose()
    {
        _server?.Dispose();
        _client.Dispose();
    }

    private static JsonElement Parse(string text) => JsonDocument.Parse(text).RootElement.Clone();

    private void Start(int dropWriteEvery)
    {
        var options = FakeJiraOptionsParser.TryParse([], out var error);
        Assert.True(options is not null, error);
        _server = new FakeJiraServer(options with { DropWriteEvery = dropWriteEvery });
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
