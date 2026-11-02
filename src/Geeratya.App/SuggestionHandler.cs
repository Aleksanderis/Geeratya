using Geeratya.Jira.Suggestions;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Views;
using Geeratya.Jira.Suggestions.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

/// <summary>
/// Turns <see cref="SearchSuggestionsEffect"/> into the commands that report it: waits out the debounce, asks Jira,
/// says "slow" when the call takes long. The waits run on the injected <see cref="TimeProvider"/>, so tests need no
/// sleeps. At most one lookup per tab lives: a newer effect, <see cref="CancelSuggestionsEffect"/> or closing the tab
/// cancels the older one, and the request id makes the reducer drop any reply that still got through. Reads only.
/// </summary>
public sealed class SuggestionHandler
{
    private const string NoConnectionMessage = "Not connected to Jira";

    private readonly NetworkRunner _runner;
    private readonly TimeProvider _time;
    private readonly object _lock = new();
    private readonly Dictionary<long, CancellationTokenSource> _pending = [];
    private SiteLabelCache? _labelCache;
    private int _labelCacheGeneration = -1;

    public SuggestionHandler(NetworkRunner runner, TimeProvider time)
    {
        _runner = runner;
        _time = time;
    }

    /// <summary>Starts the lookup (cancelling the tab's previous one) and returns the task that ends with it.</summary>
    public Task StartAsync(SearchSuggestionsEffect effect)
    {
        var current = new CancellationTokenSource();

        Replace(effect.Tab, current);

        return _runner.StartReporting(
            effect.Tab.Value,
            (services, report, token) => RunAsync(services, effect, report, current, token),
            () => new SuggestionsFailed(effect.Tab, effect.RequestId, NoConnectionMessage));
    }

    /// <summary>Stops the tab's lookup, if any (the popup closed).</summary>
    public void Cancel(TabId tab)
    {
        Replace(tab, null);
    }

    private void Replace(TabId tab, CancellationTokenSource? next)
    {
        CancellationTokenSource? old;

        lock (_lock)
        {
            _pending.Remove(tab.Value, out old);

            if (next is not null)
            {
                _pending[tab.Value] = next;
            }
        }

        old?.Cancel();
    }

    private async Task<Command?> RunAsync(
        JiraServices services,
        SearchSuggestionsEffect effect,
        Action<Command> report,
        CancellationTokenSource current,
        CancellationToken scope)
    {
        var replaced = current.Token;

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(scope, replaced);

        try
        {
            await Task.Delay(effect.Debounce, _time, linked.Token);

            return await LookUpAsync(services, effect, report, linked.Token);
        }
        catch (OperationCanceledException) when (replaced.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            Forget(effect.Tab, current);
        }
    }

    // A finished lookup leaves nothing behind unless a newer one has already taken the tab's place.
    private void Forget(TabId tab, CancellationTokenSource finished)
    {
        lock (_lock)
        {
            if (_pending.TryGetValue(tab.Value, out var latest) && ReferenceEquals(latest, finished))
            {
                _pending.Remove(tab.Value);
            }
        }
    }

    private async Task<Command?> LookUpAsync(
        JiraServices services,
        SearchSuggestionsEffect effect,
        Action<Command> report,
        CancellationToken token)
    {
        var lookup = SearchAsync(services, effect, token);

        try
        {
            await lookup.WaitAsync(SuggestionPopup.SlowAfter, _time, token);
        }
        catch (TimeoutException)
        {
            report(new SuggestionsSlow(effect.Tab, effect.RequestId));
        }

        var result = await lookup;

        if (result.IsSuccess)
        {
            return new SuggestionsLoaded(effect.Tab, effect.RequestId, result.Value);
        }

        return new SuggestionsFailed(effect.Tab, effect.RequestId, result.Error.Message);
    }

    // One label cache per connection: services are rebuilt per request, their generation changes on a switch.
    private SiteLabelCache LabelCacheFor(JiraServices services)
    {
        lock (_lock)
        {
            if (_labelCache is null || _labelCacheGeneration != services.Generation)
            {
                _labelCache = new SiteLabelCache(services.Suggestions);
                _labelCacheGeneration = services.Generation;
            }

            return _labelCache;
        }
    }

    private Task<JiraResult<IReadOnlyList<Suggestion>>> SearchAsync(
        JiraServices services,
        SearchSuggestionsEffect effect,
        CancellationToken token)
    {
        var source = effect.Source;
        var key = source.IssueKey ?? string.Empty;

        return source.Kind switch
        {
            SuggestionKind.Labels => SuggestionClient.SearchLabelsFromAsync(
                effect.Query,
                source.KnownLabels,
                cancel => LabelCacheFor(services).GetAsync(_time, cancel),
                token),
            SuggestionKind.AssignableUsers => services.Suggestions.SearchAssignableUsersAsync(key, effect.Query, token),
            SuggestionKind.IssueUsers => services.Suggestions.SearchMentionableUsersAsync(key, effect.Query, token),
            _ => services.Suggestions.SearchIssuesAsync(effect.Query, null, token),
        };
    }
}
