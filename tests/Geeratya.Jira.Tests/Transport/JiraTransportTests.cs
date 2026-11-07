using System.Net;
using System.Text;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Jira.Tests.Transport;

public sealed class JiraTransportTests
{
    private const string MyselfPath = JiraUserClient.MyselfPath;

    [Fact]
    public async Task GivenMyselfFixture_WhenGettingMyself_ThenUserIsMappedAndAuthIsBasic()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("fake-account-1", result.Value.AccountId);
        Assert.Equal("Test User 1", result.Value.DisplayName);
        var request = Assert.Single(fixture.Handler.Requests);
        Assert.Equal($"Basic {Credentials()}", request.Authorization);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal(MyselfPath, request.PathAndQuery);
        Assert.Equal(TransportFixture.Host, request.Host);
        Assert.Equal("application/json", request.Accept);
        Assert.Matches(@"^Geeratya/\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$", request.UserAgent);
        Assert.NotEqual("Geeratya/0.1", request.UserAgent);
        Assert.DoesNotContain(TransportFixture.Host, request.UserAgent, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("errors/myself-401-bad-token.json", 401, JiraErrorKind.Unauthorized)]
    [InlineData("errors/issue-delete-403-no-permission.json", 403, JiraErrorKind.Forbidden)]
    [InlineData("errors/issue-404.json", 404, JiraErrorKind.NotFound)]
    [InlineData("search/search-legacy-removed.json", 410, JiraErrorKind.NotFound)]
    [InlineData("errors/search-400-bad-jql.json", 400, JiraErrorKind.BadRequest)]
    public async Task GivenRecordedErrorBody_WhenCalling_ThenStatusMapsToTypedError(
        string fixtureName,
        int status,
        JiraErrorKind expected)
    {
        using var fixture = new TransportFixture();
        var response = FakeResponse.WithStatus((HttpStatusCode)status, FixtureFiles.Read(fixtureName));
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, response);

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.Error.Kind);
        Assert.Equal(status, result.Error.StatusCode);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenForbiddenFixture_WhenCalling_ThenMessageComesFromJiraBody()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/issue-delete-403-no-permission.json");
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.WithStatus(HttpStatusCode.Forbidden, body));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal("You do not have permission to delete issues in this project.", result.Error.Message);
    }

    [Fact]
    public async Task GivenHtmlErrorPage_WhenServerFails_ThenRetriesThenReportsServerErrorWithoutHtml()
    {
        using var fixture = new TransportFixture();
        var body = FixtureFiles.Read("errors/500-html-error-page.html");
        var response = FakeResponse.WithStatus(HttpStatusCode.InternalServerError, body);
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, response);

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.ServerError, result.Error.Kind);
        Assert.DoesNotContain("<", result.Error.Message);
        Assert.Equal(4, fixture.Handler.Requests.Count);
        Assert.Equal(3, fixture.Waits.Count);
    }

    [Fact]
    public async Task GivenRateLimitThenSuccess_WhenGetting_ThenWaitsRetryAfterAndRetries()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.RateLimited("7"));
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(7)], fixture.Waits);
    }

    [Fact]
    public async Task GivenRecorded429Reply_WhenGetting_ThenWaitsItsRetryAfterOnceAndRetries()
    {
        using var fixture = new TransportFixture();
        var headers = new Dictionary<string, string>
        {
            ["Retry-After"] = "34",
            ["ratelimit-reason"] = "jira-max-concurrent-threads-across-all-instances-per-tenant-per-user",
        };
        var recorded = new FakeResponse(
            (HttpStatusCode)429,
            FixtureFiles.Read("errors/search-429-rate-limited.json"),
            headers);
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, recorded);
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(34)], fixture.Waits);
    }

    [Fact]
    public async Task GivenRetryAfterAsHttpDate_WhenRateLimited_ThenWaitIsDateMinusNow()
    {
        var now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        using var fixture = new TransportFixture(options => options with { Clock = new FixedClock(now) });
        var date = now.AddSeconds(12).ToString("R");
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.RateLimited(date));
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal([TimeSpan.FromSeconds(12)], fixture.Waits);
    }

    [Fact]
    public async Task GivenHugeRetryAfter_WhenRateLimited_ThenReturnedAtOnceWithoutWaiting()
    {
        using var fixture = new TransportFixture(options => options with { MaxWait = TimeSpan.FromSeconds(30) });
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.RateLimited("3600"));
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(TimeSpan.FromSeconds(3600), result.Error.RetryAfter);
        Assert.Empty(fixture.Waits);
    }

    [Fact]
    public async Task GivenAlwaysRateLimited_WhenGetting_ThenGivesUpWithRateLimitedAndRetryAfter()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.RateLimited("2"));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
        Assert.Equal(TimeSpan.FromSeconds(2), result.Error.RetryAfter);
        Assert.Equal(4, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenServerErrorWithoutRetryAfter_WhenGetting_ThenBackoffGrowsExponentially()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.WithStatus(HttpStatusCode.BadGateway));

        await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(
            [TimeSpan.FromMilliseconds(375), TimeSpan.FromMilliseconds(750), TimeSpan.FromMilliseconds(1500)],
            fixture.Waits);
    }

    [Fact]
    public async Task GivenWriteRateLimitedWithoutRetryAfter_WhenSending_ThenItIsNotRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Post, "/rest/api/3/issue", FakeResponse.WithStatus((HttpStatusCode)429));
        var request = JiraRequest.Write(HttpMethod.Post, "/rest/api/3/issue", "{}");

        var result = await SendAsync(fixture, request);

        Assert.Equal(JiraErrorKind.RateLimited, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
        Assert.Empty(fixture.Waits);
    }

    [Fact]
    public async Task GivenWriteServerError_WhenSending_ThenItIsNotRetried()
    {
        using var fixture = new TransportFixture();
        var response = FakeResponse.WithStatus(HttpStatusCode.ServiceUnavailable);
        fixture.Handler.Serve(HttpMethod.Put, "/rest/api/3/issue/A-1", response);
        var request = JiraRequest.Write(HttpMethod.Put, "/rest/api/3/issue/A-1", "{}");

        var result = await SendAsync(fixture, request);

        Assert.Equal(JiraErrorKind.ServerError, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenSearchPostRateLimited_WhenSending_ThenItIsRetriedWithBody()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(HttpMethod.Post, "/rest/api/3/search/jql", FakeResponse.RateLimited("1"));
        fixture.Handler.Serve(HttpMethod.Post, "/rest/api/3/search/jql", FakeResponse.Ok(MyselfJson()));
        var request = JiraRequest.PostSearch("/rest/api/3/search/jql", "{\"jql\":\"x\"}");

        var result = await SendAsync(fixture, request);

        Assert.True(result.IsSuccess);
        Assert.All(fixture.Handler.Requests, recorded => Assert.Equal("{\"jql\":\"x\"}", recorded.Body));
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenConnectionFailureThenSuccess_WhenGetting_ThenNetworkErrorIsRetried()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.Broken());
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenPersistentConnectionFailure_WhenGetting_ThenTypedNetworkErrorNotException()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Broken());

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.Network, result.Error.Kind);
    }

    [Fact]
    public async Task GivenHangingServer_WhenTimeoutElapses_ThenTimeoutError()
    {
        using var fixture = new TransportFixture(options => options with { Timeout = TimeSpan.FromMilliseconds(50) });
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Hanging());

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.Timeout, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenHangingServer_WhenCallerCancels_ThenOperationCanceledIsThrown()
    {
        using var fixture = new TransportFixture();
        using var source = new CancellationTokenSource();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Hanging());

        var pending = new JiraUserClient(fixture.Transport).GetMyselfAsync(source.Token);
        await fixture.Handler.WaitForInFlightAsync(1);
        await source.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public async Task GivenGarbageBody_WhenStatusIsOk_ThenInvalidResponseError()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok("<html>not json</html>"));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.InvalidResponse, result.Error.Kind);
    }

    [Fact]
    public async Task GivenCapOfTwo_WhenFourCallsRunAtOnce_ThenNeverMoreThanTwoAreInFlight()
    {
        using var fixture = new TransportFixture(options => options with { MaxConcurrentRequests = 2 });
        var gate = new TaskCompletionSource();
        var reply = FakeResponse.Ok(MyselfJson()) with { Gate = gate.Task };
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, reply);
        var client = new JiraUserClient(fixture.Transport);

        var calls = Enumerable.Range(0, 4).Select(_ => client.GetMyselfAsync(CancellationToken.None)).ToList();
        await fixture.Handler.WaitForInFlightAsync(2);
        gate.SetResult();
        var results = await Task.WhenAll(calls);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, fixture.Handler.PeakInFlight);
        Assert.Equal(4, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenHostOutsideAllowlist_WhenCalling_ThenRefusedWithoutAnyRequest()
    {
        var allowed = JiraHostAllowList.Parse("dev.example.net");
        using var fixture = new TransportFixture(options => options with { AllowedHosts = allowed });

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.HostRefused, result.Error.Kind);
        Assert.Empty(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenHostInAllowlist_WhenCalling_ThenRequestIsSent()
    {
        var allowed = JiraHostAllowList.Parse(TransportFixture.Host.ToUpperInvariant());
        using var fixture = new TransportFixture(options => options with { AllowedHosts = allowed });
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task GivenTokenEchoedByServerAndFailures_WhenCalling_ThenTokenNeverAppearsInErrors()
    {
        using var fixture = new TransportFixture();
        var credentials = Credentials();
        var body = $"{{\"errorMessages\":[\"bad {TransportFixture.Token} and {credentials}\"],\"errors\":{{}}}}";
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.WithStatus(HttpStatusCode.BadRequest, body));
        fixture.Handler.Serve(HttpMethod.Get, "/other", FakeResponse.Broken());
        var client = new JiraUserClient(fixture.Transport);

        var echoed = await client.GetMyselfAsync(CancellationToken.None);
        var network = await SendAsync(fixture, JiraRequest.Get("/other"));

        foreach (var text in new[] { echoed.Error.ToString(), network.Error.ToString() })
        {
            Assert.DoesNotContain(TransportFixture.Token, text);
            Assert.DoesNotContain(credentials, text);
        }
    }

    [Fact]
    public void GivenPlainHttpRemoteSite_WhenCreatingTransport_ThenItIsRejected()
    {
        var connection = new JiraConnection(new Uri("http://example.atlassian.net"), TransportFixture.Email);
        var token = new JiraApiToken("t");

        Assert.Throws<ArgumentException>(() => new JiraTransport(connection, token, new FakeJiraHandler()));
    }

    [Fact]
    public void GivenPlainHttpLoopbackSite_WhenCreatingTransport_ThenItIsAccepted()
    {
        var connection = new JiraConnection(new Uri("http://127.0.0.1:5000"), TransportFixture.Email);

        using var transport = new JiraTransport(connection, new JiraApiToken("t"), new FakeJiraHandler());

        Assert.Equal(connection, transport.Connection);
    }

    [Fact]
    public async Task GivenRedirectResponse_WhenGetting_ThenUnexpectedErrorAndNoFollow()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.WithStatus(HttpStatusCode.Found));

        var result = await new JiraUserClient(fixture.Transport).GetMyselfAsync(CancellationToken.None);

        Assert.Equal(JiraErrorKind.Unexpected, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public void GivenLiveHostsVariable_WhenBuildingLiveTestOptions_ThenOnlyThoseHostsAreAllowed()
    {
        var options = JiraTransportOptions.ForLiveTests(_ => "dev.example.net, other.example.net");

        Assert.Equal(2, options.AllowedHosts!.Count);
        Assert.Contains("dev.example.net", options.AllowedHosts);
        Assert.Empty(JiraTransportOptions.ForLiveTests(_ => null).AllowedHosts!);
    }

    [Fact]
    public void GivenToken_WhenFormattingIt_ThenValueIsRedacted()
    {
        var token = new JiraApiToken(TransportFixture.Token);

        Assert.Equal("[redacted]", token.ToString());
        Assert.Equal("[redacted]", $"{token}");
    }

    [Fact]
    public async Task GivenSiteWithBasePath_WhenGetting_ThenPathIsAppended()
    {
        var site = new Uri("https://example.atlassian.net/ex/jira/cloud-1/");
        var connection = new JiraConnection(site, TransportFixture.Email);
        using var handler = new FakeJiraHandler();
        handler.Serve(HttpMethod.Get, $"/ex/jira/cloud-1{MyselfPath}", FakeResponse.Ok(MyselfJson()));
        using var transport = new JiraTransport(connection, new JiraApiToken("t"), handler);

        var result = await new JiraUserClient(transport).GetMyselfAsync(CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    private static string MyselfJson() => FixtureFiles.Read("myself/myself.json");

    private static string Credentials() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{TransportFixture.Email}:{TransportFixture.Token}"));

    private static Task<JiraResult<JiraUser>> SendAsync(TransportFixture fixture, JiraRequest request) =>
        fixture.Transport.SendAsync(request, JiraJsonContext.Default.JiraUser, CancellationToken.None);
}
