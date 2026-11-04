using System.Globalization;
using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.App;

public sealed partial class ViewLoadHandler
{
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
                    var found = await FindSprintAsync(services, effect, id, token);
                    report(found);

                    // The future sprints let Complete offer the next one as the destination (#504). Only an active
                    // sprint (or an unresolved one) can be completed, so a closed or future sprint skips the request.
                    if (found.Sprint is null || found.Sprint.State == SprintState.Active)
                    {
                        await ReportSprintsAsync(services, effect, id, report, token);
                    }
                }
            }

            BoardKind? probed = null;

            var switched = false;
            var probeTried = false;

            var probeBacklog = loaded is not null || NeedsRetriedProbe(services, effect, id);

            if (effect.Definition.Source == ViewSourceKind.Backlog && probeBacklog)
            {
                // A team-managed Scrum board is typed "simple", so only the sprint probe tells it from Kanban.
                probeTried = true;
                probed = await ResolveKindAsync(services, id, loaded, effect.Background, token);

                if (probed == BoardKind.Scrum)
                {
                    switched = true;
                    report(new BacklogBecameScrum(effect.Tab, effect.RequestId));
                    var scrumDefinition = effect.Definition with { Source = ViewSourceKind.ScrumBacklog };
                    effect = effect with { Definition = scrumDefinition };
                }
            }

            if (effect.Definition.Source == ViewSourceKind.ScrumBacklog && (switched || NeedsConfiguration(effect)))
            {
                await ReportSprintsAsync(services, effect, id, report, token);
            }

            // The sprint sources are Scrum by definition, so the kind needs no lookup.
            var kind = probed;

            if (effect.Definition.Source is ViewSourceKind.Sprint or ViewSourceKind.ScrumBacklog)
            {
                kind = BoardKind.Scrum;
            }
            else if (effect.Cursor is not null)
            {
                kind = services.BoardKinds.Recall(id);
            }
            else if (!probeTried)
            {
                kind = await ResolveKindAsync(services, id, loaded, effect.Background, token);
            }

            return await BoardPageAsync(services, effect, id, kind, report, token);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Failure(effect, $"Loading failed unexpectedly ({exception.GetType().Name}).");
        }
    }

    /// <summary>
    /// The sprint of a Sprint view: the named one, or the first active sprint of the board. No sprint for none active; a failed lookup is marked so the view does not claim there is none (the issues still load).
    /// </summary>
    private static async Task<SprintLoaded> FindSprintAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long boardId,
        CancellationToken token)
    {
        if (!SprintSource.TryParse(effect.Definition.SourceValue, out var source))
        {
            return new SprintLoaded(effect.Tab, effect.RequestId, null, true);
        }

        var priority = effect.Background ? JiraPriority.Background : JiraPriority.User;

        if (source.SprintId is { } sprintId)
        {
            var one = await services.Sprints.GetSprintAsync(sprintId, token, priority);

            return new SprintLoaded(effect.Tab, effect.RequestId, one.IsSuccess ? one.Value : null, !one.IsSuccess);
        }

        var active = await services.Sprints.GetActiveSprintsAsync(boardId, token, priority);

        var first = active.IsSuccess && active.Value.Count > 0 ? active.Value[0] : null;

        return new SprintLoaded(effect.Tab, effect.RequestId, first, !active.IsSuccess);
    }

    /// <summary>
    /// The open and future sprints of the board, for the section headers of the Scrum backlog (dates, goal, sprints
    /// with no issue yet) and for Complete on a Sprint view (#504). A failed list posts nothing: the sections then take their names from the issues.
    /// </summary>
    private static async Task ReportSprintsAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long boardId,
        Action<Command> report,
        CancellationToken token)
    {
        var sprints = new List<Sprint>();
        var priority = effect.Background ? JiraPriority.Background : JiraPriority.User;
        var query = new SprintQuery([SprintState.Active, SprintState.Future])
        {
            Priority = priority,
        };

        for (var page = 0; page < MaxSprintPages; page++)
        {
            var result = await services.Sprints.ListSprintsAsync(boardId, query, sprints.Count, token);

            if (!result.IsSuccess)
            {
                return;
            }

            sprints.AddRange(result.Value.Items);

            if (result.Value.IsLast || result.Value.Items.Count == 0)
            {
                break;
            }
        }

        report(new SprintsLoaded(effect.Tab, effect.RequestId, sprints));
    }

    private async Task<Command?> BoardPageAsync(
        JiraServices services,
        LoadViewPageEffect effect,
        long id,
        BoardKind? kind,
        Action<Command> report,
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

        if (effect.Definition.Source == ViewSourceKind.Board && kind == BoardKind.Kanban)
        {
            // Jira's board hides Done issues resolved long ago; the agile endpoint does not (docs/jira-layer.md).
            if (effect.DoneCutoffDays > 0)
            {
                query = query with { Jql = DoneCutoff.Jql(effect.DoneCutoffDays) };
            }

            // Reported with 0 too, so a title from an earlier load with a cutoff does not stay.
            report(new DoneCutoffApplied(effect.Tab, effect.RequestId, effect.DoneCutoffDays));
        }

        query = query with
        {
            Jql = QuickFilterJql.Combine(query.Jql, effect.ExtraJql),
            Extras = query.Extras with { IncludeSubtasks = true },
        };

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
            return sprint.SprintId is { } id ? $"sprint = {id.ToString(CultureInfo.InvariantCulture)}" : ActiveSprintJql;
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
    /// A plain backlog whose board kind is still unknown (the probe failed earlier) probes again on a refresh, so a
    /// team-managed Scrum board swaps to the Scrum backlog once Jira answers. Later pages (with a cursor) never probe.
    /// </summary>
    private static bool NeedsRetriedProbe(JiraServices services, LoadViewPageEffect effect, long id)
    {
        return effect.Cursor is null && services.BoardKinds.Recall(id) is null;
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
