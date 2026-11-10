using System.Net;
using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Comments;

public sealed class CommentWriteTests
{
    private const string ListPath = "/rest/api/3/issue/GTM-5/comment";
    private const string OnePath = "/rest/api/3/issue/GTM-5/comment/10006";

    [Fact]
    public async Task GivenText_WhenAdding_ThenAdfBodyIsPostedAndStoredCommentMapped()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus(HttpStatusCode.Created, FixtureFiles.Read("writes/comment-post-gtm-reply.json"));
        fixture.Handler.Serve(HttpMethod.Post, ListPath, reply);

        var result = await new CommentClient(fixture.Transport)
            .AddCommentAsync("GTM-5", AdfMarkdownParser.Parse("first **comment** é \U0001F600"), Cancel());

        var comment = result.Value;
        Assert.Equal("10006", comment.Id);
        Assert.Equal("Test User 1", comment.Author?.DisplayName);
        Assert.Equal("fake-account-1", comment.Author?.AccountId);
        Assert.NotNull(comment.Created);
        Assert.NotNull(comment.Updated);
        var sent = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Post, sent.Method);
        Assert.Equal(ListPath, sent.PathAndQuery);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal(["body"], body.RootElement.EnumerateObject().Select(property => property.Name));
        var doc = body.RootElement.GetProperty("body");
        Assert.Equal("doc", doc.GetProperty("type").GetString());
        Assert.Equal(1, doc.GetProperty("version").GetInt32());
        Assert.Contains("first ", doc.GetRawText());
        Assert.Contains("\"strong\"", doc.GetRawText());
        Assert.Contains("\U0001F600", doc.GetProperty("content")[0].GetProperty("content").EnumerateArray().Last().GetProperty("text").GetString());
    }

    [Fact]
    public async Task GivenAParentId_WhenAdding_ThenTheBodyCarriesItAsANumberAndTheReplyKeepsIt()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus(
            HttpStatusCode.Created,
            FixtureFiles.Read("writes/comment-post-gcm-reply-parentid.json"));
        fixture.Handler.Serve(HttpMethod.Post, ListPath, reply);

        var result = await new CommentClient(fixture.Transport)
            .ReplyToCommentAsync("GTM-5", "10029", AdfMarkdownParser.Parse("answer"), Cancel());

        Assert.Equal("10029", result.Value.ParentId);
        var sent = Assert.Single(fixture.Handler.Requests);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal(["body", "parentId"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal(10029, body.RootElement.GetProperty("parentId").GetInt32());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1/../2")]
    [InlineData("abc")]
    public async Task GivenABadParentId_WhenAdding_ThenItIsRefusedBeforeSending(string parent)
    {
        using var fixture = new TransportFixture();

        var result = await new CommentClient(fixture.Transport)
            .ReplyToCommentAsync("GTM-5", parent, AdfMarkdownParser.Parse("x"), Cancel());

        Assert.Equal(WriteErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenText_WhenEditing_ThenPutToCommentUrlReturnsUpdatedComment()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, OnePath, FakeResponse.Ok(FixtureFiles.Read("writes/comment-put-gtm-reply.json")));

        var result = await new CommentClient(fixture.Transport)
            .EditCommentAsync("GTM-5", "10006", AdfMarkdownParser.Parse("edited comment"), Cancel());

        Assert.Equal("10006", result.Value.Id);
        Assert.True(result.Value.Updated > result.Value.Created);
        var sent = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Put, sent.Method);
        Assert.Equal(OnePath, sent.PathAndQuery);
        using var body = JsonDocument.Parse(sent.Body!);
        Assert.Equal(["body"], body.RootElement.EnumerateObject().Select(property => property.Name));
        Assert.Equal("doc", body.RootElement.GetProperty("body").GetProperty("type").GetString());
        Assert.Contains("edited comment", sent.Body);
    }

    [Fact]
    public async Task GivenComment_WhenDeleting_ThenDeleteIsSentAndAccepted()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Delete, OnePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await new CommentClient(fixture.Transport).DeleteCommentAsync("GTM-5", "10006", Cancel());

        Assert.True(result.IsSuccess);
        var sent = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(HttpMethod.Delete, sent.Method);
        Assert.Null(sent.Body);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.NotFound, WriteErrorKind.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized, WriteErrorKind.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest, WriteErrorKind.Validation)]
    public async Task GivenErrorReply_WhenEditingOrDeleting_ThenTypedWriteError(HttpStatusCode status, WriteErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Put, OnePath, FakeResponse.WithStatus(status, """{"errorMessages":["nope"]}"""));
        fixture.Handler.Serve(HttpMethod.Delete, OnePath, FakeResponse.WithStatus(status, """{"errorMessages":["nope"]}"""));
        var client = new CommentClient(fixture.Transport);

        var edit = await client.EditCommentAsync("GTM-5", "10006", AdfMarkdownParser.Parse("x"), Cancel());
        var delete = await client.DeleteCommentAsync("GTM-5", "10006", Cancel());

        Assert.Equal(kind, edit.Error.Kind);
        Assert.Equal(kind, delete.Error.Kind);
        Assert.False(edit.Error.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenLostReply_WhenAdding_ThenOutcomeIsUnknownNotRetriedAndReloadRuns()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, ListPath, FakeResponse.Broken());
        fixture.Handler.Serve(HttpMethod.Get, ListPath, FakeResponse.Ok(FixtureFiles.Read("adf/comments-gtm5.json")));
        var client = new CommentClient(fixture.Transport);

        var result = await client.AddCommentAsync("GTM-5", AdfMarkdownParser.Parse("x"), Cancel());
        var reload = await ReadAfterWrite.ReloadIfUnknownAsync(
            result, token => client.GetCommentsAsync("GTM-5", 0, token), Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.Equal(1, fixture.Handler.Requests.Count(request => request.Method == HttpMethod.Post));
        Assert.True(reload!.IsSuccess);
    }

    [Fact]
    public async Task GivenRateLimitWithoutRetryAfter_WhenAdding_ThenRateLimitedAndNotRepeated()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, ListPath, FakeResponse.WithStatus(HttpStatusCode.TooManyRequests));

        var result = await new CommentClient(fixture.Transport)
            .AddCommentAsync("GTM-5", AdfMarkdownParser.Parse("x"), Cancel());

        Assert.Equal(WriteErrorKind.RateLimited, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenReplyWithoutFields_WhenAdding_ThenCommentIsEmptyButSucceeds()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, ListPath, FakeResponse.WithStatus(HttpStatusCode.Created, "{}"));

        var result = await new CommentClient(fixture.Transport)
            .AddCommentAsync("GTM-5", AdfMarkdownParser.Parse("x"), Cancel());

        Assert.Equal(string.Empty, result.Value.Id);
        Assert.Null(result.Value.Author);
        Assert.Null(result.Value.Created);
    }

    [Theory]
    [InlineData("gtm-5", "1")]
    [InlineData("GTM-5/../x", "1")]
    [InlineData("GTM-5", "")]
    [InlineData("GTM-5", "1/../2")]
    [InlineData("GTM-5", "abc")]
    public async Task GivenInvalidKeyOrId_WhenEditingOrDeleting_ThenNothingIsSent(string key, string id)
    {
        using var fixture = new TransportFixture();
        var client = new CommentClient(fixture.Transport);

        var edit = await client.EditCommentAsync(key, id, AdfMarkdownParser.Parse("x"), Cancel());
        var delete = await client.DeleteCommentAsync(key, id, Cancel());

        Assert.Equal(WriteErrorKind.NotFound, edit.Error.Kind);
        Assert.Equal(WriteErrorKind.NotFound, delete.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenEmptyText_WhenAddingOrEditing_ThenValidationErrorAndNothingIsSent()
    {
        using var fixture = new TransportFixture();
        var client = new CommentClient(fixture.Transport);

        var add = await client.AddCommentAsync("GTM-5", AdfMarkdownParser.Parse("  "), Cancel());
        var edit = await client.EditCommentAsync("GTM-5", "10006", AdfMarkdownParser.Parse(""), Cancel());

        Assert.Equal(WriteErrorKind.Validation, add.Error.Kind);
        Assert.Equal(WriteErrorKind.Validation, edit.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, WriteErrorKind.Permission)]
    [InlineData(HttpStatusCode.NotFound, WriteErrorKind.NotFound)]
    public async Task GivenErrorReply_WhenAdding_ThenTypedWriteError(HttpStatusCode status, WriteErrorKind kind)
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, ListPath, FakeResponse.WithStatus(status, "{}"));

        var result = await new CommentClient(fixture.Transport)
            .AddCommentAsync("GTM-5", AdfMarkdownParser.Parse("x"), Cancel());

        Assert.Equal(kind, result.Error.Kind);
    }

    [Fact]
    public async Task GivenInvalidKey_WhenAdding_ThenNothingIsSent()
    {
        using var fixture = new TransportFixture();

        var result = await new CommentClient(fixture.Transport)
            .AddCommentAsync("bad", AdfMarkdownParser.Parse("x"), Cancel());

        Assert.Equal(WriteErrorKind.NotFound, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    private static CancellationToken Cancel() => CancellationToken.None;
}
