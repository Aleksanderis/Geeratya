using System.Net;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Jira.Tests.Transport;

public sealed class JiraTransportSchedulingTests
{
    private const string MyselfPath = JiraUserClient.MyselfPath;
    private const string RateLimitHeader = "ratelimit";
    private const string RemainingHeader = "x-ratelimit-remaining";

    [Fact]
    public async Task GivenCapOfTwoWithOneReserved_WhenBackgroundCallsRunAtOnce_ThenOnlyOneIsInFlightUntilUserCalls()
    {
        using var fixture = new TransportFixture(options => options with { MaxConcurrentRequests = 2 });
        var gate = new TaskCompletionSource();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Gate = gate.Task });

        var background = Enumerable.Range(0, 3).Select(_ => SendAsync(fixture, BackgroundRequest())).ToList();
        await fixture.Handler.WaitForInFlightAsync(1);
        await Task.Delay(50);

        Assert.Equal(1, fixture.Handler.PeakInFlight);

        var user = SendAsync(fixture, JiraRequest.Get(MyselfPath));
        await fixture.Handler.WaitForInFlightAsync(2);
        gate.SetResult();
        var results = await Task.WhenAll(background.Append(user));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, fixture.Handler.PeakInFlight);
    }

    [Fact]
    public async Task GivenBackgroundQueuedBeforeUser_WhenSlotFrees_ThenUserRequestIsSentFirst()
    {
        using var fixture = new TransportFixture(options => options with { MaxConcurrentRequests = 1 });
        var gate = new TaskCompletionSource();
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Gate = gate.Task });
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var first = SendAsync(fixture, JiraRequest.Get($"{MyselfPath}?name=first"));
        await fixture.Handler.WaitForInFlightAsync(1);
        var background = SendAsync(fixture, JiraRequest.Get($"{MyselfPath}?name=background").AsBackground());
        await Task.Delay(50);
        var user = SendAsync(fixture, JiraRequest.Get($"{MyselfPath}?name=user"));
        await Task.Delay(50);
        gate.SetResult();
        await Task.WhenAll(first, background, user);

        var order = fixture.Handler.Requests.Select(request => request.PathAndQuery).ToList();

        Assert.Equal(
            [$"{MyselfPath}?name=first", $"{MyselfPath}?name=user", $"{MyselfPath}?name=background"],
            order);
    }

    [Fact]
    public async Task GivenQueuedCallCancelled_WhenSlotFrees_ThenItIsSkippedAndNextCallRuns()
    {
        using var fixture = new TransportFixture(options => options with { MaxConcurrentRequests = 1 });
        var gate = new TaskCompletionSource();
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Gate = gate.Task });
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));
        using var cancellation = new CancellationTokenSource();

        var first = SendAsync(fixture, JiraRequest.Get(MyselfPath));
        await fixture.Handler.WaitForInFlightAsync(1);
        var cancelled = fixture.Transport.SendAsync(
            BackgroundRequest(),
            JiraJsonContext.Default.JiraUser,
            cancellation.Token);
        var third = SendAsync(fixture, BackgroundRequest());
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        gate.SetResult();

        Assert.True((await first).IsSuccess);
        Assert.True((await third).IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenFewRequestsLeft_WhenBackgroundCallFollows_ThenItWaitsForTheWindowButUserCallDoesNot()
    {
        using var fixture = NewFixture();
        var limited = FakeResponse.Ok(MyselfJson()) with { Headers = Header(RateLimitHeader, Policy(3, 2)) };
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, limited);

        await SendAsync(fixture, JiraRequest.Get(MyselfPath));
        await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.Empty(fixture.Waits);

        await SendAsync(fixture, BackgroundRequest());

        Assert.Equal([TimeSpan.FromSeconds(2)], fixture.Waits);
        Assert.Equal(3, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenNoRequestsLeft_WhenUserCallFollows_ThenItWaitsForTheWindowOnce()
    {
        using var fixture = NewFixture();
        var exhausted = FakeResponse.Ok(MyselfJson()) with { Headers = Header(RateLimitHeader, Policy(0, 1.5)) };
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, exhausted);

        await SendAsync(fixture, JiraRequest.Get(MyselfPath));
        await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.Equal([TimeSpan.FromSeconds(1.5)], fixture.Waits);
    }

    [Fact]
    public async Task GivenPlentyLeft_WhenCallingInBackground_ThenNothingWaits()
    {
        using var fixture = NewFixture();
        var relaxed = FakeResponse.Ok(MyselfJson()) with { Headers = Header(RateLimitHeader, Policy(99, 1)) };
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, relaxed);

        await SendAsync(fixture, BackgroundRequest());
        await SendAsync(fixture, BackgroundRequest());

        Assert.Empty(fixture.Waits);
    }

    [Fact]
    public async Task GivenOnlyRemainingHeader_WhenBackgroundCallFollowsLowCount_ThenItWaitsOneSecond()
    {
        using var fixture = NewFixture();
        var legacy = FakeResponse.Ok(MyselfJson()) with { Headers = Header(RemainingHeader, "4") };
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, legacy);

        await SendAsync(fixture, BackgroundRequest());
        await SendAsync(fixture, BackgroundRequest());

        Assert.Equal([TimeSpan.FromSeconds(1)], fixture.Waits);
    }

    [Fact]
    public async Task GivenSeveralPolicies_WhenReadingHeader_ThenTightestPolicyDecides()
    {
        using var fixture = NewFixture();
        var value = $"{Policy(50, 1)}, \"jira-quota-global-based\";r=2;t=30";
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Headers = Header(RateLimitHeader, value) });

        await SendAsync(fixture, BackgroundRequest());
        await SendAsync(fixture, BackgroundRequest());

        Assert.Equal([TimeSpan.FromSeconds(30)], fixture.Waits);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("\"jira-burst-based\";r=abc;t=1")]
    [InlineData("\"jira-burst-based\";r=-1;t=1")]
    public async Task GivenUnreadableHeader_WhenCalling_ThenNoThrottleAndCallSucceeds(string value)
    {
        using var fixture = NewFixture();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Headers = Header(RateLimitHeader, value) });

        var first = await SendAsync(fixture, BackgroundRequest());
        var second = await SendAsync(fixture, BackgroundRequest());

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Empty(fixture.Waits);
    }

    [Fact]
    public async Task GivenTooManyRequestsReply_WhenHeadersSayNoneLeft_ThenRetryAfterIsHonouredWithoutDoubleWait()
    {
        using var fixture = NewFixture();
        var limited = new FakeResponse(
            (HttpStatusCode)429,
            Headers: new Dictionary<string, string> { ["Retry-After"] = "3", [RateLimitHeader] = Policy(0, 3) });
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, limited);
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, fixture.Handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(3)], fixture.Waits);
    }

    private static TransportFixture NewFixture() =>
        new(options => options with { Clock = new FixedClock(new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero)) });

    private static JiraRequest BackgroundRequest() => JiraRequest.Get(MyselfPath).AsBackground();

    private static string Policy(int remaining, double resetSeconds) =>
        FormattableString.Invariant($"\"jira-burst-based\";r={remaining};t={resetSeconds}");

    private static Dictionary<string, string> Header(string name, string value) => new() { [name] = value };

    private static string MyselfJson() => FixtureFiles.Read("myself/myself.json");

    private static Task<JiraResult<JiraUser>> SendAsync(TransportFixture fixture, JiraRequest request) =>
        fixture.Transport.SendAsync(request, JiraJsonContext.Default.JiraUser, CancellationToken.None);
}
