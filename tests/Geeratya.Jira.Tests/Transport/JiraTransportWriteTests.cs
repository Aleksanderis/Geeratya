using System.Net;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Tests.Transport;

public sealed class JiraTransportWriteTests
{
    private const string WritePath = "/rest/api/3/issue";
    private const string Body = "{\"fields\":{}}";

    [Fact]
    public async Task GivenCreatedReply_WhenWriting_ThenValueIsParsedAndBodyIsSentOnce()
    {
        using var fixture = new TransportFixture();
        var reply = FakeResponse.WithStatus(HttpStatusCode.Created, "{\"accountId\":\"fake-account-1\"}");
        fixture.Handler.Serve(HttpMethod.Post, WritePath, reply);

        var result = await fixture.Transport.SendWriteAsync(Request(), JiraJsonContext.Default.JiraUser, Cancel());

        Assert.Equal("fake-account-1", result.Value.AccountId);
        var sent = Assert.Single(fixture.Handler.Requests);
        Assert.Equal(Body, sent.Body);
    }

    [Fact]
    public async Task GivenNoContentReply_WhenWritingWithoutResult_ThenSuccess()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GivenReadRequest_WhenSendingAsWrite_ThenArgumentException()
    {
        using var fixture = new TransportFixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Transport.SendWriteAsync(JiraRequest.Get(WritePath), Cancel()));
    }

    [Fact]
    public async Task GivenFieldErrors_WhenWriting_ThenValidationKeepsEveryFieldById()
    {
        using var fixture = new TransportFixture();
        const string body = """
            {"errorMessages":["General problem"],"errors":{"summary":"Too long","issuetype":"Bad"}}
            """;
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.Validation, result.Error.Kind);
        Assert.Equal(["General problem"], result.Error.Messages);
        Assert.Contains(new WriteFieldError("summary", "Too long"), result.Error.FieldErrors);
        Assert.Contains(new WriteFieldError("issuetype", "Bad"), result.Error.FieldErrors);
        Assert.False(result.Error.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenRecordedFieldValidation_WhenWriting_ThenFieldErrorIsKeyedById()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/create-400-bad-issuetype.json");
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        var field = Assert.Single(result.Error.FieldErrors);
        Assert.Equal("issuetype", field.FieldId);
        Assert.Equal("Specify a valid issue type", field.Message);
        Assert.Empty(result.Error.Messages);
    }

    [Theory]
    [InlineData("errors/myself-401-bad-token.json", 401, WriteErrorKind.Unauthorized)]
    [InlineData("errors/issue-delete-403-no-permission.json", 403, WriteErrorKind.Permission)]
    [InlineData("errors/issue-404.json", 404, WriteErrorKind.NotFound)]
    [InlineData("errors/transition-409-conflict.json", 409, WriteErrorKind.Conflict)]
    public async Task GivenRecordedErrorBody_WhenWriting_ThenStatusMapsToWriteError(
        string fixtureName,
        int status,
        WriteErrorKind expected)
    {
        using var fixture = new TransportFixture();
        var response = FakeResponse.WithStatus((HttpStatusCode)status, FixtureFiles.Read(fixtureName));
        fixture.Handler.Serve(HttpMethod.Post, WritePath, response);

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(expected, result.Error.Kind);
        Assert.Equal(status, result.Error.StatusCode);
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenConflictBody_WhenWriting_ThenMessageIsJirasOwn()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/transition-409-conflict.json");
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus(HttpStatusCode.Conflict, body));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal("The issue was changed by someone else. Reload and try again.", result.Error.Message);
    }

    [Fact]
    public async Task GivenServerError_WhenWriting_ThenUnknownOutcomeAndNotRetried()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/500-html-error-page.html");
        var reply = FakeResponse.WithStatus(HttpStatusCode.InternalServerError, body);
        fixture.Handler.Serve(HttpMethod.Post, WritePath, reply);

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.UnknownOutcome, result.Error.Kind);
        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.DoesNotContain("<", result.Error.Message);
        Assert.Single(fixture.Handler.Requests);
        Assert.Empty(fixture.Waits);
    }

    [Fact]
    public async Task GivenConnectionFailure_WhenWriting_ThenUnknownOutcomeAndNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.Broken());

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.UnknownOutcome, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenUnreadableSuccessBody_WhenWriting_ThenUnknownOutcome()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.Ok("not json"));

        var result = await fixture.Transport.SendWriteAsync(Request(), JiraJsonContext.Default.JiraUser, Cancel());

        Assert.True(result.Error.IsOutcomeUnknown);
    }

    [Fact]
    public async Task GivenHangingServer_WhenTimeoutElapses_ThenTimeoutIsUnknownOutcomeAndNotRetried()
    {
        using var fixture = new TransportFixture(options => options with { Timeout = TimeSpan.FromMilliseconds(50) });
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.Hanging());

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.Timeout, result.Error.Kind);
        Assert.True(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenHangingServer_WhenCallerCancels_ThenOperationCanceledIsThrown()
    {
        using var fixture = new TransportFixture();
        using var source = new CancellationTokenSource();
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.Hanging());

        var pending = fixture.Transport.SendWriteAsync(Request(), source.Token);
        await fixture.Handler.WaitForInFlightAsync(1);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenRateLimitWithRetryAfter_WhenWriting_ThenWaitsAndSucceedsOnSecondRequest()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(HttpMethod.Post, WritePath, FakeResponse.RateLimited("7"));
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus(HttpStatusCode.NoContent));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(7)], fixture.Waits);
    }

    [Fact]
    public async Task GivenRateLimitWithoutRetryAfter_WhenWriting_ThenNotRetried()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/search-429-rate-limited.json");
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus((HttpStatusCode)429, body));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.RateLimited, result.Error.Kind);
        Assert.False(result.Error.IsOutcomeUnknown);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenRateLimitForever_WhenWriting_ThenGivesUpWithRetryAfterAfterMaxRetries()
    {
        using var fixture = new TransportFixture(options => options with { MaxRetries = 2 });
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.RateLimited("3"));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(3), result.Error.RetryAfter);
        Assert.Equal(3, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenDisallowedHost_WhenWriting_ThenNotSentAndNothingRequested()
    {
        using var fixture = new TransportFixture(options => options with { AllowedHosts = new HashSet<string>() });

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.NotSent, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenTokenInErrorBody_WhenWriting_ThenMessagesAndFieldErrorsAreRedacted()
    {
        using var fixture = new TransportFixture();
        var token = TransportFixture.Token;
        var body = $$$"""{"errorMessages":["first","bad {{{token}}}"],"errors":{"summary":"echo {{{token}}}"}}""";
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.DoesNotContain(token, result.Error.Message);
        Assert.All(result.Error.Messages, message => Assert.DoesNotContain(token, message));
        Assert.All(result.Error.FieldErrors, field => Assert.DoesNotContain(token, field.Message));
        Assert.Contains("[redacted]", Assert.Single(result.Error.FieldErrors).Message);
    }

    [Fact]
    public async Task GivenRateLimitBeyondMaxWait_WhenWriting_ThenRateLimitedAtOnceWithoutWaiting()
    {
        using var fixture = new TransportFixture(options => options with { MaxWait = TimeSpan.FromSeconds(30) });
        fixture.Handler.Serve(HttpMethod.Post, WritePath, FakeResponse.RateLimited("120"));

        var result = await fixture.Transport.SendWriteAsync(Request(), Cancel());

        Assert.Equal(WriteErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(120), result.Error.RetryAfter);
        Assert.Single(fixture.Handler.Requests);
        Assert.Empty(fixture.Waits);
    }

    [Fact]
    public async Task GivenRateLimitBeyondMaxWait_WhenReading_ThenRateLimitedAtOnceWithoutWaiting()
    {
        using var fixture = new TransportFixture(options => options with { MaxWait = TimeSpan.FromSeconds(30) });
        fixture.Handler.Serve(HttpMethod.Get, WritePath, FakeResponse.RateLimited("120"));

        var result = await fixture.Transport.SendAsync(
            JiraRequest.Get(WritePath), JiraJsonContext.Default.JiraUser, Cancel());

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
        Assert.Empty(fixture.Waits);
    }

    private static JiraRequest Request() => JiraRequest.Write(HttpMethod.Post, WritePath, Body);

    private static CancellationToken Cancel() => CancellationToken.None;
}
