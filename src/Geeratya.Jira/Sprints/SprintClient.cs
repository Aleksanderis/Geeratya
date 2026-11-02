using System.Globalization;
using Geeratya.Jira.Boards;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Reads sprints over the agile API: a board's sprints by state, one sprint, and the issues of a sprint. Reads only
/// (sprint writes are a separate client). Never call <see cref="ListSprintsAsync"/> for a board known to be Kanban
/// (Jira answers 400): ask <see cref="BoardKindResolver"/> first.
/// </summary>
public sealed class SprintClient(JiraTransport transport)
{
    /// <summary>The largest sprint page Jira serves; larger <c>maxResults</c> values are clamped to it.</summary>
    public const int MaxPageSize = 50;

    private const string BoardPath = "/rest/agile/1.0/board";
    private const string SprintPath = "/rest/agile/1.0/sprint";
    private const string SprintSuffix = "sprint";
    private const string IssueSuffix = "issue";

    public async Task<JiraResult<OffsetPage<Sprint>>> ListSprintsAsync(
        long boardId,
        SprintQuery query,
        int startAt,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>
        {
            $"startAt={Number(Math.Max(startAt, 0))}",
            $"maxResults={Number(Math.Clamp(query.PageSize, 1, MaxPageSize))}",
        };

        var states = StateNames(query.States);

        if (states.Length > 0)
        {
            parts.Add($"state={states}");
        }

        var url = $"{BoardPath}/{Number(boardId)}/{SprintSuffix}?{string.Join('&', parts)}";
        var request = JiraRequest.Get(url) with { Priority = query.Priority };
        var result = await transport.SendAsync(request, SprintJsonContext.Default.SprintList, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(SprintMapper.MapList(result.Value))
            : JiraResults.Failure<OffsetPage<Sprint>>(result.Error);
    }

    public async Task<JiraResult<Sprint>> GetSprintAsync(
        long sprintId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var request = JiraRequest.Get($"{SprintPath}/{Number(sprintId)}") with { Priority = priority };
        var result = await transport.SendAsync(request, SprintJsonContext.Default.Sprint, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(SprintMapper.MapSprint(result.Value))
            : JiraResults.Failure<Sprint>(result.Error);
    }

    /// <summary>
    /// The active sprints of a Scrum board: none, one, or several (parallel sprints are allowed), in Jira's order.
    /// Walks the pages, so a failure on any page fails the whole call.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<Sprint>>> GetActiveSprintsAsync(
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var query = new SprintQuery([SprintState.Active]) { Priority = priority };
        var sprints = new List<Sprint>();
        var startAt = 0;

        while (true)
        {
            var result = await ListSprintsAsync(boardId, query, startAt, cancellationToken);

            if (!result.IsSuccess)
            {
                return JiraResults.Failure<IReadOnlyList<Sprint>>(result.Error);
            }

            sprints.AddRange(result.Value.Items);

            if (result.Value.NextStartAt is not { } next)
            {
                return JiraResults.Success<IReadOnlyList<Sprint>>(sprints);
            }

            startAt = next;
        }
    }

    /// <summary>
    /// Loads one page of a sprint's issues in rank order, any sprint state. Pass the previous <c>Next</c>, null for
    /// the first page.
    /// </summary>
    public async Task<JiraResult<IssuePage>> GetIssuesAsync(
        long sprintId,
        BoardIssueQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken)
    {
        var startAt = 0;

        if (cursor is not null && !cursor.TryGetOffset(out startAt))
        {
            return JiraResults.Failure<IssuePage>(
                new JiraError(JiraErrorKind.BadRequest, null, "The page cursor does not belong to a sprint."));
        }

        var pageSize = Math.Clamp(query.PageSize, 1, BoardClient.MaxPageSize);
        var paging = $"startAt={Number(startAt)}&maxResults={Number(pageSize)}";
        var url = $"{SprintPath}/{Number(sprintId)}/{IssueSuffix}?fields={IssueFields.ListRowQueryValue}&{paging}";

        if (!string.IsNullOrWhiteSpace(query.Jql))
        {
            url += $"&jql={Uri.EscapeDataString(query.Jql)}";
        }

        var request = JiraRequest.Get(url) with { Priority = query.Priority };
        var result = await transport.SendAsync(request, SprintJsonContext.Default.IssuePage, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapIssues(result.Value))
            : JiraResults.Failure<IssuePage>(result.Error);
    }

    private static string StateNames(IReadOnlyList<SprintState>? states)
    {
        if (states is null)
        {
            return string.Empty;
        }

        var names = states.Select(SprintMapper.StateName).Where(name => name.Length > 0).Distinct();

        return string.Join(',', names);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
