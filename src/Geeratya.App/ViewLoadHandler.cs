using System.Globalization;
using Geeratya.Core.Cache;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

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

    private const string OpenSprintsJql = "sprint in openSprints()";

    // Active and future sprints plus the backlog; closed sprints (thousands of done issues) are left out.
    private const string BacklogWithSprintsJql =
        "sprint in openSprints() or sprint in futureSprints() or sprint is EMPTY";
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
        if (!TryBoardId(effect.Definition, out var id))
        {
            return Failure(effect, InvalidBoardMessage);
        }

        try
        {
            BoardConfiguration? loaded = null;

            if (NeedsConfiguration(effect))
            {
                var configuration = await services.Boards.GetConfigurationAsync(id, token);

                if (!configuration.IsSuccess)
                {
                    return new ViewLoadFailed(effect.Tab, effect.RequestId, configuration.Error, _clock.GetUtcNow());
                }

                loaded = configuration.Value;
                services.Fields.RememberEstimate(id, loaded.EstimationFieldId);
                report(new BoardConfigurationLoaded(effect.Tab, effect.RequestId, loaded));

                if (effect.Definition.Source == ViewSourceKind.Sprint)
                {
                    report(new SprintLoaded(effect.Tab, effect.RequestId, await FindSprintAsync(services, effect, id, token)));
                }
            }

            // The sprint sources are Scrum by definition, so the kind needs no lookup.
            var kind = effect.Definition.Source is ViewSourceKind.Sprint or ViewSourceKind.ScrumBacklog
                ? BoardKind.Scrum
                : await ResolveKindAsync(services, id, loaded, effect.Background, token);

            return await BoardPageAsync(services, effect, id, kind, token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(effect, $"Loading failed unexpectedly ({exception.GetType().Name}).");
        }
    }

    /// <summary>
    /// The sprint of a Sprint view: the named one, or the first active sprint of the board. Null for no active
    /// sprint or a failed lookup (the header is then missing, the issues still load).
    /// </summary>
    private static async Task<Sprint?> FindSprintAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long boardId,
        CancellationToken token)
    {
        if (!SprintSource.TryParse(effect.Definition.SourceValue, out var source))
        {
            return null;
        }

        var priority = effect.Background ? JiraPriority.Background : JiraPriority.User;

        if (source.SprintId is { } sprintId)
        {
            var one = await services.Sprints.GetSprintAsync(sprintId, token, priority);

            return one.IsSuccess ? one.Value : null;
        }

        var active = await services.Sprints.GetActiveSprintsAsync(boardId, token, priority);

        return active.IsSuccess && active.Value.Count > 0 ? active.Value[0] : null;
    }

    private async Task<Command?> BoardPageAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long id,
        BoardKind? kind,
        CancellationToken token)
    {
        var query = new BoardIssueQuery();

        // The sprint sources exist only for Scrum boards, so they read sprints even when the kind probe failed.
        var scrum = kind == BoardKind.Scrum
            || effect.Definition.Source is ViewSourceKind.Sprint or ViewSourceKind.ScrumBacklog;

        if (scrum)
        {
            query = await ScrumQueryAsync(services, effect, id, query, token);
        }

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
    /// Scrum or Kanban for the board, from its type, the stored answer or one probe of the sprint list (remembered per
    /// profile). Null when the probe failed: the board then loads as it did before sprints, and nothing is stored.
    /// </summary>
    private static async Task<BoardKind?> ResolveKindAsync(
        JiraServices services,
        long id,
        BoardConfiguration? configuration,
        bool background,
        CancellationToken token)
    {
        var priority = background ? JiraPriority.Background : JiraPriority.User;
        BoardSummary? board;

        if (configuration is not null)
        {
            board = new BoardSummary(id, configuration.Name, configuration.Type, string.Empty);
        }
        else if (services.BoardKinds.Recall(id) is { } recalled)
        {
            return recalled;
        }
        else
        {
            var found = await services.Boards.GetBoardAsync(id, token, priority);
            board = found.IsSuccess ? found.Value : null;
        }

        if (board is null)
        {
            return null;
        }

        var resolved = await services.BoardKinds.ResolveAsync(board, token, priority);

        if (!resolved.IsSuccess)
        {
            return null;
        }

        // Kept in memory so later pages and refreshes of this board need neither the configuration nor a request.
        services.BoardKinds.Remember(id, resolved.Value);

        return resolved.Value;
    }

    /// <summary>
    /// A Scrum board shows the open sprints (the board endpoint lists every sprint state and the backlog), with the
    /// Sprint and estimate fields read onto the rows; the backlog source keeps what the backlog endpoint returns.
    /// Without the field ids the rows simply carry no sprint or estimate.
    /// </summary>
    private static async Task<BoardIssueQuery> ScrumQueryAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long id,
        BoardIssueQuery query,
        CancellationToken token)
    {
        var priority = effect.Background ? JiraPriority.Background : JiraPriority.User;
        var fields = await services.Fields.GetForBoardAsync(id, token, priority);
        var withFields = fields.IsSuccess ? query with { Extras = fields.Value } : query;

        return withFields with { Jql = SprintJql(effect.Definition) };
    }

    /// <summary>
    /// The sprint filter of a source: open sprints for a board, one sprint or the open ones for a sprint view, none for
    /// the backlog endpoint, and the open and future sprints plus the backlog for the Scrum backlog.
    /// </summary>
    private static string? SprintJql(ViewDefinition definition)
    {
        if (definition.Source == ViewSourceKind.Board)
        {
            return OpenSprintsJql;
        }

        if (definition.Source == ViewSourceKind.ScrumBacklog)
        {
            return BacklogWithSprintsJql;
        }

        if (definition.Source == ViewSourceKind.Sprint
            && SprintSource.TryParse(definition.SourceValue, out var sprint))
        {
            return sprint.SprintId is { } id ? $"sprint = {id.ToString(CultureInfo.InvariantCulture)}" : OpenSprintsJql;
        }

        return null;
    }

    private static bool TryBoardId(ViewDefinition definition, out long id)
    {
        if (definition.Source == ViewSourceKind.Sprint)
        {
            var parsed = SprintSource.TryParse(definition.SourceValue, out var sprint);
            id = sprint.BoardId;

            return parsed;
        }

        return long.TryParse(definition.SourceValue, NumberStyles.None, CultureInfo.InvariantCulture, out id);
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
