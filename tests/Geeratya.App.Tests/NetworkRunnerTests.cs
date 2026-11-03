using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public sealed class NetworkRunnerTests
{
    private static readonly Profile _a = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly Profile _b = new("b", "https://b.atlassian.net", "me@b.test");
    private static readonly Command _reply = new TextPasted("reply");

    [Fact]
    public async Task GivenReplyAfterProfileSwitch_WhenItArrives_ThenNothingIsPosted()
    {
        using var fixture = await ConnectedAsync(_a, _b);
        var gate = new TaskCompletionSource();

        var run = fixture.Runner.Start(1, async (_, _) =>
        {
            await gate.Task;

            return _reply;
        });

        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenCurrentProfile_WhenReplyArrives_ThenItIsPostedOnce()
    {
        using var fixture = await ConnectedAsync(_a);

        await fixture.Runner.Start(1, (_, _) => Task.FromResult<Command?>(_reply));

        Assert.Equal([_reply], fixture.Posted);
    }

    [Fact]
    public async Task GivenReadAndWriteInFlight_WhenCancellingReads_ThenOnlyTheReadIsCancelled()
    {
        using var fixture = await ConnectedAsync(_a);
        var gate = new TaskCompletionSource();
        var readToken = new TaskCompletionSource<CancellationToken>();
        var writeToken = new TaskCompletionSource<CancellationToken>();

        var read = fixture.Runner.Start(1, async (_, token) =>
        {
            readToken.SetResult(token);
            await gate.Task;

            return _reply;
        });

        var write = fixture.Runner.StartDetached(async (_, token) =>
        {
            writeToken.SetResult(token);
            await gate.Task;

            return _reply;
        });

        var seenRead = await readToken.Task;
        var seenWrite = await writeToken.Task;

        fixture.Runner.CancelReads();
        gate.SetResult();
        await Task.WhenAll(read, write);

        Assert.True(seenRead.IsCancellationRequested);
        Assert.False(seenWrite.IsCancellationRequested);
        Assert.Equal([_reply], fixture.Posted);
    }

    [Fact]
    public async Task GivenPendingWork_WhenCancellingAll_ThenTokenIsCancelledAndNothingPosted()
    {
        using var fixture = await ConnectedAsync(_a);
        var gate = new TaskCompletionSource();
        var started = new TaskCompletionSource<CancellationToken>();

        var run = fixture.Runner.Start(1, async (_, token) =>
        {
            started.SetResult(token);
            await gate.Task;

            return _reply;
        });

        var seen = await started.Task;

        fixture.Runner.CancelAll();
        gate.SetResult();
        await run;

        Assert.True(seen.IsCancellationRequested);
        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenTwoTabs_WhenCancellingOne_ThenTheOtherStillPosts()
    {
        using var fixture = await ConnectedAsync(_a);
        var gate = new TaskCompletionSource();

        var first = fixture.Runner.Start(1, async (_, _) =>
        {
            await gate.Task;

            return new TextPasted("one");
        });

        var second = fixture.Runner.Start(2, async (_, _) =>
        {
            await gate.Task;

            return new TextPasted("two");
        });

        fixture.Runner.CancelScope(1);
        gate.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal([new TextPasted("two")], fixture.Posted);
    }

    [Fact]
    public async Task GivenCancelledTab_WhenStartingAgain_ThenNewWorkIsNotCancelled()
    {
        using var fixture = await ConnectedAsync(_a);

        fixture.Runner.CancelScope(1);
        await fixture.Runner.Start(1, (_, _) => Task.FromResult<Command?>(_reply));

        Assert.Equal([_reply], fixture.Posted);
    }

    [Fact]
    public async Task GivenCancelledAll_WhenStartingAgain_ThenNewWorkIsNotCancelled()
    {
        using var fixture = await ConnectedAsync(_a);

        fixture.Runner.CancelAll();
        await fixture.Runner.Start(1, (_, _) => Task.FromResult<Command?>(_reply));

        Assert.Equal([_reply], fixture.Posted);
    }

    [Fact]
    public async Task GivenWorkCancelledByItsScope_WhenRunning_ThenNothingIsPostedAndNoThrow()
    {
        using var fixture = await ConnectedAsync(_a);

        await fixture.Runner.Start(1, (_, token) =>
        {
            fixture.Runner.CancelScope(1);
            token.ThrowIfCancellationRequested();

            return Task.FromResult<Command?>(_reply);
        });

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenWorkThatFailsAfterSwitch_WhenItThrows_ThenNothingIsPostedAndNoThrow()
    {
        using var fixture = await ConnectedAsync(_a, _b);
        var gate = new TaskCompletionSource();

        var run = fixture.Runner.Start(1, async (_, _) =>
        {
            await gate.Task;

            throw new ObjectDisposedException("transport");
        });

        await fixture.Connections.ActivateAsync(_b, 2, CancellationToken.None);
        gate.SetResult();
        await run;

        Assert.Empty(fixture.Posted);
    }

    [Fact]
    public async Task GivenWorkThatFailsOnCurrentSite_WhenItThrows_ThenTheReturnedTaskFaults()
    {
        using var fixture = await ConnectedAsync(_a);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Runner.Start(1, (_, _) => throw new InvalidOperationException()));
    }

    [Fact]
    public async Task GivenNoTransport_WhenStarting_ThenWorkNeverRuns()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        var posted = new List<Command>();
        var ran = false;

        using var runner = new NetworkRunner(connections, new ListCommandSink(posted), CancellationToken.None);

        await runner.Start(1, (_, _) =>
        {
            ran = true;

            return Task.FromResult<Command?>(_reply);
        });

        Assert.False(ran);
        Assert.Empty(posted);
    }

    [Fact]
    public async Task GivenNoTransport_WhenStartingLocalWork_ThenItRunsAndIsPosted()
    {
        using var connections = new SiteConnections(
            new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)));

        var posted = new List<Command>();

        using var runner = new NetworkRunner(connections, new ListCommandSink(posted), CancellationToken.None);

        await runner.StartLocal(1, _ => _reply);

        Assert.Same(_reply, Assert.Single(posted));
    }

    [Fact]
    public async Task GivenLocalWorkThatThrows_WhenStarting_ThenTheTaskDoesNotFaultAndNothingIsPosted()
    {
        using var fixture = await ConnectedAsync(_a);

        await fixture.Runner.StartLocal(1, _ => throw new InvalidOperationException());

        Assert.Empty(fixture.Posted);
    }

    private static async Task<Fixture> ConnectedAsync(params Profile[] profiles)
    {
        var store = new FakeSecretStore();

        foreach (var profile in profiles)
        {
            await store.SetAsync(profile.CredentialKey, $"token-{profile.Name}", CancellationToken.None);
        }

        var connections = new SiteConnections(new TokenProvider(store, new EnvironmentReader(_ => null)));

        await connections.ActivateAsync(profiles[0], profiles.Length, CancellationToken.None);

        return new Fixture(connections);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture(SiteConnections connections)
        {
            Connections = connections;
            Runner = new NetworkRunner(connections, new ListCommandSink(Posted), CancellationToken.None);
        }

        public SiteConnections Connections { get; }

        public NetworkRunner Runner { get; }

        public List<Command> Posted { get; } = [];

        public void Dispose()
        {
            Runner.Dispose();
            Connections.Dispose();
        }
    }
}
