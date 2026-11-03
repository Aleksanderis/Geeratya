using Geeratya.Core.Cache;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.App;

/// <summary>
/// Turns a <see cref="LoadViewPageEffect"/> into the command that reports it: <see cref="ViewPageLoaded"/> or
/// <see cref="ViewLoadFailed"/>, both carrying the effect's request id. The work runs on the
/// <see cref="NetworkRunner"/> under the tab's scope, so closing the tab cancels it and a reply from an older site is dropped before it is posted.
/// One method per source kind; a new source adds a case, not new loading code.
/// </summary>
public sealed partial class ViewLoadHandler
{
    private const string NoConnectionMessage =
        "Not connected to Jira: check the profile and its API token (:profile), then reload.";

    private const string OpenSprintsJql = "sprint in openSprints()";

    // Jira has no activeSprints(): open minus future leaves the active sprints, so a planned sprint never mixes in.
    private const string ActiveSprintJql = "sprint in openSprints() and sprint not in futureSprints()";

    // Active and future sprints plus the backlog; closed sprints (thousands of done issues) are left out.
    private const string BacklogWithSprintsJql =
        "sprint in openSprints() or sprint in futureSprints() or sprint is EMPTY";

    private const string UnsupportedSourceMessage = "This kind of view cannot be loaded yet.";

    private const int MaxSprintPages = 10;

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
        // The cache holds the board as a whole, so a load narrowed by quick filters never reads it.
        if (cache is not null && effect.Reason == LoadReason.Open && effect.Cursor is null && effect.ExtraJql is null)
        {
            await _runner.StartLocal(effect.Tab.Value, token => ReadCache(effect, cache, token));
        }

        await StartNetwork(effect);
    }

    /// <summary>
    /// Reads the quick filters of the tab's board (<c>view.quick-filter</c>, #192) and posts them; a failure posts
    /// the message for the status line. Only asked for when a filter key is used, so opening a board costs no extra
    /// request.
    /// </summary>
    public Task StartQuickFiltersAsync(LoadQuickFiltersEffect effect)
    {
        return _runner.Start(
            effect.Tab.Value,
            (services, token) => QuickFiltersAsync(services, effect, token),
            () => new QuickFiltersLoaded(effect.Tab, [], effect.Apply, NoConnectionMessage));
    }

    private static async Task<Command?> QuickFiltersAsync(
        JiraServices services,
        LoadQuickFiltersEffect effect,
        CancellationToken token)
    {
        if (!TryBoardId(effect.Definition, out var id))
        {
            return new QuickFiltersLoaded(effect.Tab, [], effect.Apply, InvalidBoardMessage);
        }

        try
        {
            var result = await services.Boards.GetQuickFiltersAsync(id, token);

            return result.IsSuccess
                ? new QuickFiltersLoaded(effect.Tab, result.Value, effect.Apply)
                : new QuickFiltersLoaded(effect.Tab, [], effect.Apply, LoadErrorText.For(result.Error));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var text = $"Loading the quick filters failed unexpectedly ({exception.GetType().Name}).";

            return new QuickFiltersLoaded(effect.Tab, [], effect.Apply, text);
        }
    }

    private Task StartNetwork(LoadViewPageEffect effect)
    {
        var jql = ViewQuery.JqlFor(effect.Definition);

        if (effect.Definition.Source.IsBoardBased())
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

        if (view is null && effect.Definition.Source == ViewSourceKind.Backlog)
        {
            // A team-managed Scrum backlog opens as a plain backlog and is cached under the Scrum key.
            var scrumKey = CacheKey.For(effect.Definition with { Source = ViewSourceKind.ScrumBacklog });
            var scrum = cache.Get(scrumKey);

            return scrum is null ? null : new ViewCacheLoaded(effect.Tab, effect.RequestId, scrum, true);
        }

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
}
