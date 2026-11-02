using System.Net;
using Geeratya.Core.Cache;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;
using Geeratya.App.Tests.Fakes;

namespace Geeratya.App.Tests;

public sealed class ViewLoadHandlerCacheTests : ICommandSink, IDisposable
{
    private const string Fresh =
        """{"issues":[{"id":"9","key":"A-9","fields":{"summary":"Fresh"}}],"isLast":true}""";

    private static readonly Profile _profile = new("a", "https://a.atlassian.net", "me@a.test");
    private static readonly CacheKey _key = new(ViewSourceKind.Jql, "project = A");
    private static readonly TabId _tab = new(7);

    private readonly string _root = Path.Combine(Path.GetTempPath(), $"geeratya-load-{Guid.NewGuid():N}");
    private readonly List<Command> _posted = [];
    private readonly object _postedLock = new();
    private readonly TaskCompletionSource _cachePosted = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<IDisposable> _disposables = [];
    private NetworkRunner? _runner;
    private SiteConnections? _connections;

    [Fact]
    public async Task GivenCachedFirstPageOnDisk_WhenOpeningWithTheNetworkStillAnswering_ThenCachedRowsArriveBeforeTheReply()
    {
        var cache = SecondStartCache();
        var gate = new TaskCompletionSource();
        var handler = await HandlerAsync(connected: true, replyGate: gate.Task);

        var run = handler.StartAsync(Open(), cache);
        await _cachePosted.Task.WaitAsync(TimeSpan.FromSeconds(30));

        var before = Snapshot();
        gate.SetResult();
        await run;

        var cached = Assert.IsType<ViewCacheLoaded>(Assert.Single(before));
        Assert.Equal(["A-1", "A-2"], cached.View.Rows.Select(row => row.Key));
        Assert.Equal(11, cached.RequestId);
        Assert.Equal(_tab, cached.Tab);

        var after = Snapshot();
        Assert.IsType<ViewCacheLoaded>(after[0]);
        Assert.Equal(["A-9"], Assert.IsType<ViewPageLoaded>(after[1]).Page.Items.Select(row => row.Key));
    }

    [Fact]
    public async Task GivenCachedPageAndNoConnection_WhenOpening_ThenTheCachedRowsAreStillPostedBeforeTheFailure()
    {
        var cache = SecondStartCache();
        var handler = await HandlerAsync(connected: false, replyGate: null);

        await handler.StartAsync(Open(), cache);

        var posted = Snapshot();
        Assert.IsType<ViewCacheLoaded>(posted[0]);
        Assert.IsType<ViewLoadFailed>(posted[1]);
    }

    [Fact]
    public async Task GivenNoCachedPage_WhenOpening_ThenOnlyTheNetworkReplyIsPosted()
    {
        var cache = new SiteCache(Path.Combine(_root, "empty"));
        _disposables.Add(cache);
        var handler = await HandlerAsync(connected: true, replyGate: null);

        await handler.StartAsync(Open(), cache);

        Assert.IsType<ViewPageLoaded>(Assert.Single(Snapshot()));
    }

    [Fact]
    public async Task GivenCachedPage_WhenRefreshingNotOpening_ThenTheCacheIsNotRead()
    {
        var cache = SecondStartCache();
        var handler = await HandlerAsync(connected: true, replyGate: null);
        var refresh = Open() with { Reason = LoadReason.Refresh };

        await handler.StartAsync(refresh, cache);

        Assert.IsType<ViewPageLoaded>(Assert.Single(Snapshot()));
    }

    public void Dispose()
    {
        _runner?.Dispose();
        _connections?.Dispose();

        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    void ICommandSink.Post(Command command)
    {
        lock (_postedLock)
        {
            _posted.Add(command);
        }

        if (command is ViewCacheLoaded)
        {
            _cachePosted.TrySetResult();
        }
    }

    private static LoadViewPageEffect Open()
    {
        var definition = new ViewDefinition(ViewSourceKind.Jql, "project = A", ViewLayout.List, "A");

        return new LoadViewPageEffect(_tab, definition, null, 11, LoadReason.Open, false);
    }

    private static IssueSummary Issue(int number)
    {
        return new IssueSummary(
            $"{number}",
            $"A-{number}",
            $"Issue {number}",
            new IssueStatus("1", "To Do", "new"),
            new IssueTypeSummary("10", "Task", 0, false),
            null,
            null,
            [],
            null,
            null,
            "A");
    }

    /// <summary>A cache object that reads what an earlier run wrote to disk: the "second start".</summary>
    private SiteCache SecondStartCache()
    {
        var folder = Path.Combine(_root, "p");

        using (var earlier = new SiteCache(folder))
        {
            earlier.Put(_key, new CachedView([Issue(1), Issue(2)], null, DateTimeOffset.UnixEpoch));
            earlier.Flush();
        }

        var cache = new SiteCache(folder);

        _disposables.Add(cache);

        return cache;
    }

    private async Task<ViewLoadHandler> HandlerAsync(bool connected, Task? replyGate)
    {
        var http = new ScriptedJiraHandler().Reply(HttpStatusCode.OK, Fresh, replyGate);
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

        if (connected)
        {
            await _connections.ActivateAsync(_profile, 1, CancellationToken.None);
        }

        _runner = new NetworkRunner(_connections, this, CancellationToken.None);

        return new ViewLoadHandler(_runner);
    }

    private List<Command> Snapshot()
    {
        lock (_postedLock)
        {
            return [.. _posted];
        }
    }
}
