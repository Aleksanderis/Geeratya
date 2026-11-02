using Geeratya.Core.Cache;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Tui;

/// <summary>
/// Turns a <see cref="LoadViewPageEffect"/> into the command that reports it: <see cref="ViewPageLoaded"/> or
/// <see cref="ViewLoadFailed"/>, both carrying the effect's request id. The work runs on the
/// <see cref="NetworkRunner"/> under the tab's scope, so closing the tab cancels it and a reply from an older site is dropped before it is posted.
/// One method per source kind; a new source adds a case, not new loading code.
/// </summary>
public sealed class ViewLoadHandler
{
    private const string NoConnectionMessage =
        "Not connected to Jira: check the profile and its API token (:profile), then reload.";

    private const string UnsupportedSourceMessage = "This kind of view cannot be loaded yet.";

    private readonly NetworkRunner _runner;
    private readonly TimeProvider _clock;

    public ViewLoadHandler(NetworkRunner runner, TimeProvider? clock = null)
    {
        _runner = runner;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts the load and returns at once with the task that ends when it is posted (or dropped). An opening load
    /// first answers from <paramref name="cache"/> (off the UI thread, no connection needed) and only then asks
    /// Jira, so the cached rows are always posted before any reply or failure. Without a connection the tab is told
    /// so instead of loading forever.
    /// </summary>
    public async Task StartAsync(LoadViewPageEffect effect, SiteCache? cache = null)
    {
        if (cache is not null && effect.Reason == LoadReason.Open && effect.Cursor is null)
        {
            await _runner.StartLocal(effect.Tab.Value, token => ReadCache(effect, cache, token));
        }

        await StartNetwork(effect);
    }

    private Task StartNetwork(LoadViewPageEffect effect)
    {
        var jql = ViewQuery.JqlFor(effect.Definition);

        if (jql is null)
        {
            return _runner.Start(
                effect.Tab.Value,
                (_, _) => Task.FromResult<Command?>(Failure(effect, UnsupportedSourceMessage)),
                () => Failure(effect, UnsupportedSourceMessage));
        }

        return _runner.Start(
            effect.Tab.Value,
            (services, token) => SearchAsync(services, effect, jql, token),
            () => Failure(effect, NoConnectionMessage));
    }

    private static ViewCacheLoaded? ReadCache(LoadViewPageEffect effect, SiteCache cache, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return null;
        }

        var view = cache.Get(CacheKey.For(effect.Definition));

        return view is null ? null : new ViewCacheLoaded(effect.Tab, effect.RequestId, view);
    }

    private async Task<Command?> SearchAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        string jql,
        CancellationToken token)
    {
        var query = new IssueSearchQuery(jql, ViewPaging.PageSize);

        if (effect.Background)
        {
            query = query.AsBackground();
        }

        try
        {
            var result = await services.Search.SearchAsync(query, effect.Cursor, token);

            return result.IsSuccess
                ? new ViewPageLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow())
                : new ViewLoadFailed(effect.Tab, effect.RequestId, result.Error, _clock.GetUtcNow());
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(effect, $"Loading failed unexpectedly ({exception.GetType().Name}).");
        }
    }

    private ViewLoadFailed Failure(LoadViewPageEffect effect, string message)
    {
        var error = new JiraError(JiraErrorKind.Unexpected, null, message);

        return new ViewLoadFailed(effect.Tab, effect.RequestId, error, _clock.GetUtcNow());
    }
}
