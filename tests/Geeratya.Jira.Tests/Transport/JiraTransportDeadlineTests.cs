using System.Diagnostics;
using System.Net;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Users;

namespace Geeratya.Jira.Tests.Transport;

public sealed class JiraTransportDeadlineTests
{
    private const string MyselfPath = JiraUserClient.MyselfPath;

    [Fact]
    public async Task GivenEverySlotHeldAndABackgroundCallHung_WhenUserCallWaits_ThenBackgroundIsAbandonedAndUserCompletes()
    {
        using var fixture = new TransportFixture(options => options with
        {
            MaxConcurrentRequests = 2,
            Timeout = TimeSpan.FromSeconds(30),
            OperationDeadline = TimeSpan.FromSeconds(30),
            PreemptBackgroundAfter = TimeSpan.FromMilliseconds(50),
        });
        var never = new TaskCompletionSource();
        var hung = FakeResponse.Ok(MyselfJson()) with { Gate = never.Task };
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, hung);
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, hung);
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));
        using var holderCancellation = new CancellationTokenSource();

        var background = SendAsync(fixture, JiraRequest.Get(MyselfPath).AsBackground());
        await fixture.Handler.WaitForInFlightAsync(1);
        var holder = fixture.Transport.SendAsync(
            JiraRequest.Get(MyselfPath),
            JiraJsonContext.Default.JiraUser,
            holderCancellation.Token);
        await fixture.Handler.WaitForInFlightAsync(2);

        var started = Stopwatch.GetTimestamp();
        var user = await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.True(user.IsSuccess);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(10));

        await holderCancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => holder);

        // The abandoned background call is a read: it retries once and then succeeds.
        Assert.True((await background).IsSuccess);
    }

    [Fact]
    public async Task GivenOnlyUserCallsHung_WhenAnotherUserCallWaits_ThenNothingIsAbandonedAndItEndsAtTheDeadline()
    {
        using var fixture = new TransportFixture(options => options with
        {
            MaxConcurrentRequests = 1,
            Timeout = TimeSpan.FromSeconds(30),
            OperationDeadline = TimeSpan.FromMilliseconds(300),
            PreemptBackgroundAfter = TimeSpan.FromMilliseconds(50),
        });
        var never = new TaskCompletionSource();
        fixture.Handler.Script(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Gate = never.Task });
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var hung = SendAsync(fixture, JiraRequest.Get(MyselfPath));
        await fixture.Handler.WaitForInFlightAsync(1);

        var started = Stopwatch.GetTimestamp();
        var waiting = await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.Equal(JiraErrorKind.Timeout, waiting.Error.Kind);
        Assert.Contains("did not finish", waiting.Error.Message, StringComparison.Ordinal);
        Assert.True(Stopwatch.GetElapsedTime(started) < TimeSpan.FromSeconds(10));
        Assert.Equal(JiraErrorKind.Timeout, (await hung).Error.Kind);
    }

    [Fact]
    public async Task GivenOperationDeadlineShorterThanAttempt_WhenServerHangs_ThenCallFailsAtTheDeadlineWithoutRetry()
    {
        using var fixture = new TransportFixture(options => options with
        {
            Timeout = TimeSpan.FromSeconds(30),
            OperationDeadline = TimeSpan.FromMilliseconds(200),
        });
        var never = new TaskCompletionSource();
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()) with { Gate = never.Task });

        var result = await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.Equal(JiraErrorKind.Timeout, result.Error.Kind);
        Assert.Single(fixture.Handler.Requests);
    }

    [Fact]
    public async Task GivenRetryAfterWaitLongerThanTheDeadline_WhenItEnds_ThenTheCallStillSucceeds()
    {
        using var fixture = new TransportFixture(options => options with
        {
            OperationDeadline = TimeSpan.FromMilliseconds(200),
            Delay = static (_, token) => Task.Delay(TimeSpan.FromMilliseconds(500), token),
        });
        fixture.Handler.Script(
            HttpMethod.Get,
            MyselfPath,
            new FakeResponse((HttpStatusCode)429, Headers: new Dictionary<string, string> { ["Retry-After"] = "3" }));
        fixture.Handler.Serve(HttpMethod.Get, MyselfPath, FakeResponse.Ok(MyselfJson()));

        var result = await SendAsync(fixture, JiraRequest.Get(MyselfPath));

        Assert.True(result.IsSuccess);
    }

    private static string MyselfJson() => FixtureFiles.Read("myself/myself.json");

    private static Task<JiraResult<JiraUser>> SendAsync(TransportFixture fixture, JiraRequest request) =>
        fixture.Transport.SendAsync(request, JiraJsonContext.Default.JiraUser, CancellationToken.None);
}
