using Geeratya.App.Tests.Fakes;
using Geeratya.Core.BoardKinds;
using Geeratya.Core.Cache;
using Geeratya.Core.Detail;
using Geeratya.Core.History;
using Geeratya.Core.Profiles;
using Geeratya.Core.Startup;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

/// <summary>
/// A load that throws must end as a failure the reducer can show, never as silence (#924): the view load, the detail
/// load and a local effect, each with an unexpected exception and with a cancellation nobody asked for.
/// </summary>
public sealed class LoadFaultTests : IDisposable
{
    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly TabId _tab = new(7);

    private readonly List<Command> _posted = [];
    private readonly ScriptedJiraHandler _http = new();
    private SiteConnections? _connections;
    private NetworkRunner? _runner;

    public void Dispose()
    {
        _runner?.Dispose();
        _connections?.Dispose();
    }

    public static TheoryData<Exception> Faults()
    {
        return [new InvalidOperationException("boom"), new TaskCanceledException("not asked for")];
    }

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task GivenViewLoadThrows_WhenLoading_ThenTheTabGetsAFailureWithItsRequestId(Exception fault)
    {
        await ConnectAsync(_http.Throw(fault));
        var handler = new ViewLoadHandler(_runner!);

        await handler.StartAsync(new LoadViewPageEffect(_tab, Jql(), null, 11, LoadReason.Open, false));

        var failed = Assert.IsType<ViewLoadFailed>(Assert.Single(_posted));
        Assert.Equal(11, failed.RequestId);
        Assert.Equal(_tab, failed.Tab);
    }

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task GivenDetailLoadThrows_WhenLoading_ThenTheDetailGetsAFailureWithItsRequestId(Exception fault)
    {
        await ConnectAsync(_http.Throw(fault));
        var handler = new IssueDetailLoadHandler(_runner!);

        await handler.StartAsync(new LoadIssueDetailEffect(_tab, "A-1", 21));

        var failed = Assert.IsType<IssueDetailFailed>(Assert.Single(_posted));
        Assert.Equal(21, failed.RequestId);
        Assert.Equal(_tab, failed.Tab);
    }

    [Fact]
    public async Task GivenQuietDetailReadThrows_WhenLoading_ThenNothingIsPostedAndNothingFaults()
    {
        await ConnectAsync(_http.Throw(new InvalidOperationException("boom")));
        var handler = new IssueDetailLoadHandler(_runner!);

        await handler.StartAsync(new LoadSubtaskAssigneesEffect(_tab, "A-1"));

        Assert.Empty(_posted);
    }

    [Theory]
    [MemberData(nameof(Faults))]
    public async Task GivenWorkThrows_WhenAFaultCommandIsGiven_ThenItIsPostedOnce(Exception fault)
    {
        await ConnectAsync(_http);
        var reply = new TextPasted("faulted");

        await _runner!.Start(1, (_, _) => throw fault, whenFaulted: _ => reply);

        Assert.Equal([reply], _posted);
    }

    [Fact]
    public async Task GivenWorkThrowsAfterTheProfileChanged_WhenFaulted_ThenNothingIsPosted()
    {
        await ConnectAsync(_http);
        var gate = new TaskCompletionSource();

        var run = _runner!.Start(
            1,
            async (_, _) =>
            {
                await gate.Task;

                throw new InvalidOperationException("late");
            },
            whenFaulted: _ => new TextPasted("faulted"));

        _runner.CancelAll();
        gate.SetResult();
        await run;

        Assert.Empty(_posted);
    }

    [Fact]
    public async Task GivenCacheReadStuck_WhenWaitingForIt_ThenTheWaitEndsAfterTheLimit()
    {
        await ConnectAsync(_http);
        var gate = new TaskCompletionSource();
        var read = _runner!.StartLocal(1, _ => WaitFor(gate));

        await NetworkRunner.AwaitCacheReadAsync(read).WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(read.IsCompleted);
        gate.SetResult();
        await read;
    }

    [Fact]
    public async Task GivenLocalEffectThrows_WhenRunByTheRunner_ThenItsFaultCommandIsPosted()
    {
        await ConnectAsync(_http);
        var sink = new ListCommandSink(_posted);
        var directory = Directory.CreateTempSubdirectory("geeratya-fault-");

        try
        {
            var profiles = new ProfileServices(
                new ProfileStore(directory.FullName),
                new TokenProvider(new FakeSecretStore(), new EnvironmentReader(_ => null)),
                new SiteCaches(Path.Combine(directory.FullName, "cache")),
                new HistoryStore(directory.FullName),
                new BoardKindStore(directory.FullName));
            using var runner = new EffectRunner(profiles, _connections!, _runner!, sink, CancellationToken.None);
            runner.AddLocalHandler(new ThrowingTabsHandler());

            runner.Run(new LoadTabsEffect(_profile), _profile);
            await WaitUntilAsync(() => _posted.Count == 1);

            var loaded = Assert.IsType<TabsLoaded>(Assert.Single(_posted));
            Assert.Empty(loaded.Tabs.Items);
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static Command? WaitFor(TaskCompletionSource gate)
    {
        gate.Task.Wait();

        return null;
    }

    private static ViewDefinition Jql()
    {
        return new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        while (!condition())
        {
            await Task.Delay(1, timeout.Token);
        }
    }

    private async Task ConnectAsync(ScriptedJiraHandler http)
    {
        var store = new FakeSecretStore();

        await store.SetAsync(_profile.CredentialKey, "token-a", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        _connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(http, options));
        await _connections.ActivateAsync(_profile, 1, CancellationToken.None);
        _runner = new NetworkRunner(_connections, new ListCommandSink(_posted), CancellationToken.None);
    }

    private sealed class ThrowingTabsHandler : ILocalEffectHandler
    {
        public bool CanRun(Effect effect)
        {
            return effect is LoadTabsEffect;
        }

        public Task<Command?> RunAsync(Effect effect, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("unreadable");
        }

        public Command? Faulted(Effect effect, Exception exception)
        {
            return new TabsLoaded(((LoadTabsEffect)effect).Profile, RememberedTabs.Empty);
        }
    }
}
