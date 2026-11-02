using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
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
    private const string InvalidBoardMessage = "The board id is not a number.";

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

        if (effect.Definition.Source is ViewSourceKind.Board or ViewSourceKind.Backlog)
        {
            return _runner.StartReporting(
                effect.Tab.Value,
                (services, report, token) => BoardAsync(services, effect, report, token),
                () => Failure(effect, NoConnectionMessage));
        }

        if (jql is null)
        {
            return _runner.Start(
                effect.Tab.Value,
                (_, _) => Task.FromResult<Command?>(Failure(effect, UnsupportedSourceMessage)),
                () => Failure(effect, UnsupportedSourceMessage));
        }

        return _runner.StartReporting(
            effect.Tab.Value,
            (services, report, token) => SearchAsync(services, effect, jql, report, token),
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
        Action<Command> report,
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

            if (!result.IsSuccess)
            {
                return new ViewLoadFailed(effect.Tab, effect.RequestId, result.Error, _clock.GetUtcNow());
            }

            var page = new ViewPageLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow());

            if (effect.Cursor is not null || result.Value.IsLast)
            {
                return page;
            }

            report(page);

            return await CountAsync(services, effect, query, token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(effect, $"Loading failed unexpectedly ({exception.GetType().Name}).");
        }
    }

    /// <summary>
    /// The approximate count, asked after the first page was posted and only when more pages exist. A failure (or a
    /// throw) posts nothing: the header keeps <c>N+</c>.
    /// </summary>
    private static async Task<Command?> CountAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        IssueSearchQuery query,
        CancellationToken token)
    {
        try
        {
            var count = await services.Search.ApproximateCountAsync(query, token);

            return count.IsSuccess ? new ViewTotalLoaded(effect.Tab, count.Value) : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// A board or backlog page. The first page of an opening load fetches the board configuration first and reports
    /// it ahead of the issues; when the configuration fails, the failure is the answer and no issues are requested.
    /// </summary>
    private async Task<Command?> BoardAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        Action<Command> report,
        CancellationToken token)
    {
        if (!long.TryParse(effect.Definition.SourceValue, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
        {
            return Failure(effect, InvalidBoardMessage);
        }

        try
        {
            if (NeedsConfiguration(effect))
            {
                var configuration = await services.Boards.GetConfigurationAsync(id, token);

                if (!configuration.IsSuccess)
                {
                    return new ViewLoadFailed(effect.Tab, effect.RequestId, configuration.Error, _clock.GetUtcNow());
                }

                report(new BoardConfigurationLoaded(effect.Tab, effect.RequestId, configuration.Value));
            }

            return await BoardPageAsync(services, effect, id, token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(effect, $"Loading failed unexpectedly ({exception.GetType().Name}).");
        }
    }

    private async Task<Command?> BoardPageAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long id,
        CancellationToken token)
    {
        var query = new BoardIssueQuery();

        if (effect.Background)
        {
            query = query.AsBackground();
        }

        var result = effect.Definition.Source == ViewSourceKind.Backlog
            ? await services.Boards.GetBacklogAsync(id, query, effect.Cursor, token)
            : await services.Boards.GetIssuesAsync(id, query, effect.Cursor, token);

        return result.IsSuccess
            ? new ViewPageLoaded(effect.Tab, effect.RequestId, result.Value, _clock.GetUtcNow())
            : new ViewLoadFailed(effect.Tab, effect.RequestId, result.Error, _clock.GetUtcNow());
    }

    /// <summary>
    /// Only an opening (or restarting) load and a refresh by hand fetch the configuration; a refresh by the clock
    /// keeps the one in the tab.
    /// </summary>
    private static bool NeedsConfiguration(LoadViewPageEffect effect)
    {
        if (effect.Cursor is not null)
        {
            return false;
        }

        var byHand = effect.Reason == LoadReason.Refresh && !effect.Background;

        return effect.Reason is LoadReason.Open or LoadReason.Retry || byHand;
    }

    private ViewLoadFailed Failure(LoadViewPageEffect effect, string message)
    {
        var error = new JiraError(JiraErrorKind.Unexpected, null, message);

        return new ViewLoadFailed(effect.Tab, effect.RequestId, error, _clock.GetUtcNow());
    }
}
