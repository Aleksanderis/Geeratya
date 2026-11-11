using Geeratya.App.Tests.Fakes;
using Geeratya.Core.Profiles;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;
using Geeratya.Platform;
using Geeratya.Platform.Secrets;

namespace Geeratya.App.Tests;

/// <summary>
/// Cancelling a lookup and replacing the transport run callbacks that block here; the thread that asked must neither
/// run them nor wait for them (#926).
/// </summary>
[Trait("Os", "Independent")]
public sealed class CancelOffUiThreadTests : IDisposable
{
    private static readonly TimeSpan _limit = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _debounce = TimeSpan.FromMilliseconds(250);
    private static readonly Profile _work = new("work", "https://work.atlassian.net", "me@work.test");
    private static readonly Profile _dev = new("dev", "https://dev.atlassian.net", "me@dev.test");
    private static readonly TabId _tab = new(1);
    private static readonly SuggestionSource _assignee = new(SuggestionKind.AssignableUsers, "A-1", []);

    private readonly CancelProbeHandler _http = new();
    private readonly ManualTimeProvider _time = new();
    private readonly List<Command> _posted = [];
    private SiteConnections? _connections;
    private NetworkRunner? _runner;

    public void Dispose()
    {
        _http.Release();
        _runner?.Dispose();
        _connections?.Dispose();
        _http.Dispose();
    }

    [Fact]
    public async Task GivenALookupOnTheWire_WhenTheTabCancels_ThenTheTokenCallbackRunsOnAnotherThreadAndCancelReturns()
    {
        await ConnectAsync();

        var handler = new SuggestionHandler(_runner!, _time);
        var effect = new SearchSuggestionsEffect(_tab, 1, _assignee, "jo", _debounce);
        var task = handler.StartAsync(effect);

        await WaitUntilAsync(() => _time.PendingTimers >= 1);
        _time.Advance(_debounce);
        await _http.RequestArrived.WaitAsync(_limit);

        // The caller is a dedicated thread, not a pool thread: after it finishes, the pool could not reuse its id for
        // the callback, which would make the comparison below depend on timing.
        var caller = 0;
        var cancelling = Task.Factory.StartNew(
            () =>
            {
                caller = Environment.CurrentManagedThreadId;
                handler.Cancel(_tab);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        // Cancel has returned while the callback is still blocked: it did not run here, and it did not hold us up.
        await cancelling.WaitAsync(_limit);

        var callbackThread = await _http.CallbackThread.WaitAsync(_limit);

        Assert.NotEqual(caller, callbackThread);

        _http.Release();
        await task.WaitAsync(_limit);
    }

    [Fact]
    public async Task GivenARequestOnTheWire_WhenSwitchingProfile_ThenTheOldTransportIsDisposedOffTheCallingThread()
    {
        await ConnectAsync();

        var lookup = StartLookup();

        await _http.RequestArrived.WaitAsync(_limit);

        var caller = Environment.CurrentManagedThreadId;
        var switching = _connections!.ActivateAsync(_dev, 2, CancellationToken.None);
        var callbackThread = await _http.CallbackThread.WaitAsync(_limit);

        // The disposing thread is stuck in the callback, yet the connections answer and the switch completes.
        Assert.NotEqual(caller, callbackThread);
        Assert.Null(await switching.WaitAsync(_limit));
        Assert.Equal(new Uri(_dev.SiteUrl), _connections.Current!.Connection.SiteUrl);

        _http.Release();
        await lookup.WaitAsync(_limit);
    }

    [Fact]
    public async Task GivenAnOldTransportBlockedWhileDisposing_WhenReadingCurrent_ThenItAnswersAtOnce()
    {
        await ConnectAsync();

        var lookup = StartLookup();

        await _http.RequestArrived.WaitAsync(_limit);

        var dropping = Task.Run(() => _connections!.Drop(_work));

        await dropping.WaitAsync(TimeSpan.FromSeconds(5));
        await _http.CallbackThread.WaitAsync(_limit);

        // The disposal is blocked right now; these take the lock the disposal used to hold.
        Assert.Null(_connections!.Current);
        Assert.Equal(2, _connections.Generation);
        Assert.False(_connections.IsCurrent(1));

        _http.Release();
        await lookup.WaitAsync(_limit);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var limit = new CancellationTokenSource(_limit);

        while (!condition())
        {
            await Task.Delay(1, limit.Token);
        }
    }

    private Task<JiraResult<IReadOnlyList<Suggestion>>> StartLookup()
    {
        var client = new SuggestionClient(_connections!.Current!);

        return client.SearchAssignableUsersAsync("A-1", "jo", CancellationToken.None);
    }

    private async Task ConnectAsync()
    {
        var store = new FakeSecretStore();

        await store.SetAsync(_work.CredentialKey, "token-work", CancellationToken.None);
        await store.SetAsync(_dev.CredentialKey, "token-dev", CancellationToken.None);

        var options = new JiraTransportOptions
        {
            MaxRetries = 0,
            Delay = (_, _) => Task.CompletedTask,
            Jitter = () => 0.5,
        };

        _connections = new SiteConnections(
            new TokenProvider(store, new EnvironmentReader(_ => null)),
            new FakeTransportFactory(_http, options));
        _runner = new NetworkRunner(_connections, new ListCommandSink(_posted), CancellationToken.None);

        await _connections.ActivateAsync(_work, 2, CancellationToken.None);
    }
}
