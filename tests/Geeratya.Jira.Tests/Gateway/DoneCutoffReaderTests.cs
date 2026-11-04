using System.Net;
using Geeratya.Jira.Gateway;
using Geeratya.Jira.Tests.Fakes;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Tests.Gateway;

public sealed class DoneCutoffReaderTests
{
    private static readonly DateTimeOffset _start = new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task GivenTheRecordedChain_WhenReading_ThenTheCutoffComesFromTheViewOfTheBoard()
    {
        using var fixture = Serving("navigation-gcm4", "cutoff-gcm4");
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport));

        var result = await reader.ReadAsync(4, CancellationToken.None);

        Assert.Equal(14, result.Value.Days);
        Assert.Equal(3, fixture.Handler.Requests.Count);
        Assert.Equal(BoardViewClient.TenantPath, fixture.Handler.Requests.First().PathAndQuery);
        var cutoffRequest = fixture.Handler.Requests.Last();
        Assert.Contains(":view/activation/", cutoffRequest.Body);
        Assert.Contains("/board/4/board", cutoffRequest.Body);
    }

    [Fact]
    public async Task GivenAnAnswerRead_WhenReadingAgainWithinItsLifetime_ThenNoRequestIsSent()
    {
        using var fixture = Serving("navigation-gcm4", "cutoff-gcm4");
        var clock = new AdjustableClock(_start);
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport), clock);
        await reader.ReadAsync(4, CancellationToken.None);

        clock.Advance(DoneCutoffReader.ValueLifetime - TimeSpan.FromSeconds(1));
        var again = await reader.ReadAsync(4, CancellationToken.None);

        Assert.Equal(14, again.Value.Days);
        Assert.Equal(3, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenAnAnswerThatExpired_WhenReadingAgain_ThenOnlyTheCutoffIsReadNotTheWholeChain()
    {
        using var fixture = Serving("navigation-gcm4", "cutoff-gcm4");
        fixture.Handler.Script(
            HttpMethod.Post,
            BoardViewClient.GraphQlPath,
            FakeResponse.Ok("""{"data":{"node":{"completedIssueSearchCutOffInDays":7}}}"""));
        var clock = new AdjustableClock(_start);
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport), clock);
        await reader.ReadAsync(4, CancellationToken.None);

        clock.Advance(DoneCutoffReader.ValueLifetime + TimeSpan.FromSeconds(1));
        var again = await reader.ReadAsync(4, CancellationToken.None);

        Assert.Equal(7, again.Value.Days);
        Assert.Equal(4, fixture.Handler.Requests.Count);
        Assert.Equal(1, fixture.Handler.Requests.Count(request => request.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task GivenTwoBoards_WhenReadingBoth_ThenTheCloudIdIsAskedOnce()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Ok(Gateway("tenant-info")));
        fixture.Handler.Script(
            HttpMethod.Post,
            BoardViewClient.GraphQlPath,
            FakeResponse.Ok(Gateway("navigation-gcm4")),
            FakeResponse.Ok(Gateway("cutoff-gcm4")),
            FakeResponse.Ok(Gateway("navigation-gtm3")),
            FakeResponse.Ok(Gateway("cutoff-gtm3")));
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport));

        await reader.ReadAsync(4, CancellationToken.None);
        await reader.ReadAsync(3, CancellationToken.None);

        Assert.Equal(1, fixture.Handler.Requests.Count(request => request.Method == HttpMethod.Get));
        Assert.Equal(4, fixture.Handler.Requests.Count(request => request.Method == HttpMethod.Post));
    }

    [Fact]
    public async Task GivenAFailure_WhenReadingAgainWithinAMinute_ThenItIsNotAskedAgainButIsAfterwards()
    {
        using var fixture = new TransportFixture(options => options with { MaxRetries = 0 });
        fixture.Handler.Script(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.WithStatus(HttpStatusCode.Forbidden));
        fixture.Handler.Script(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Ok(Gateway("tenant-info")));
        fixture.Handler.Script(
            HttpMethod.Post,
            BoardViewClient.GraphQlPath,
            FakeResponse.Ok(Gateway("navigation-gcm4")),
            FakeResponse.Ok(Gateway("cutoff-gcm4")));
        var clock = new AdjustableClock(_start);
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport), clock);

        var first = await reader.ReadAsync(4, CancellationToken.None);
        var second = await reader.ReadAsync(4, CancellationToken.None);
        clock.Advance(DoneCutoffReader.FailureLifetime + TimeSpan.FromSeconds(1));
        var third = await reader.ReadAsync(4, CancellationToken.None);

        Assert.Equal(JiraErrorKind.Forbidden, first.Error.Kind);
        Assert.Equal(JiraErrorKind.Forbidden, second.Error.Kind);
        Assert.Equal(14, third.Value.Days);
        Assert.Equal(4, fixture.Handler.Requests.Count);
    }

    [Fact]
    public async Task GivenAServerThatNeverAnswers_WhenReading_ThenTheLookupTimesOutAsAFailureNotAnException()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Hanging());
        using var reader = new DoneCutoffReader(
            new BoardViewClient(fixture.Transport),
            null,
            TimeSpan.FromMilliseconds(50));

        var result = await reader.ReadAsync(4, CancellationToken.None);

        Assert.Equal(JiraErrorKind.Timeout, result.Error.Kind);
    }

    [Fact]
    public async Task GivenACallerThatCancels_WhenReading_ThenTheCancellationPropagates()
    {
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Hanging());
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(4, source.Token));
    }

    [Fact]
    public async Task GivenTwoLoadsAtOnce_WhenReadingTheSameBoard_ThenTheChainIsRequestedOnce()
    {
        var gate = new TaskCompletionSource();
        using var fixture = new TransportFixture();
        fixture.Handler.Serve(
            HttpMethod.Get,
            BoardViewClient.TenantPath,
            FakeResponse.Ok(Gateway("tenant-info")) with { Gate = gate.Task });
        fixture.Handler.Script(
            HttpMethod.Post,
            BoardViewClient.GraphQlPath,
            FakeResponse.Ok(Gateway("navigation-gcm4")),
            FakeResponse.Ok(Gateway("cutoff-gcm4")));
        using var reader = new DoneCutoffReader(new BoardViewClient(fixture.Transport));

        var first = reader.ReadAsync(4, CancellationToken.None);
        var second = reader.ReadAsync(4, CancellationToken.None);
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(14, result.Value.Days));
        Assert.Equal(3, fixture.Handler.Requests.Count);
    }

    private static string Gateway(string name)
    {
        return FixtureFiles.Read($"gateway/{name}.json");
    }

    private static TransportFixture Serving(string navigation, string cutoff)
    {
        var fixture = new TransportFixture();
        fixture.Handler.Serve(HttpMethod.Get, BoardViewClient.TenantPath, FakeResponse.Ok(Gateway("tenant-info")));
        fixture.Handler.Script(
            HttpMethod.Post,
            BoardViewClient.GraphQlPath,
            FakeResponse.Ok(Gateway(navigation)),
            FakeResponse.Ok(Gateway(cutoff)));

        return fixture;
    }

    private sealed class AdjustableClock(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset _now = start;

        public void Advance(TimeSpan span)
        {
            _now += span;
        }

        public override DateTimeOffset GetUtcNow() => _now;
    }
}
