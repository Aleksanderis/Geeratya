using System.Globalization;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Boards;

/// <summary>
/// Reads boards over the agile API (<c>/rest/agile/1.0/</c>): list, one board, configuration, board issues and
/// backlog. Reads only. Sprint endpoints are deliberately absent: a Kanban board answers them with 400, and Scrum
/// sprints belong to a later milestone. The backlog is whatever the endpoint returns for the board kind.
/// </summary>
public sealed class BoardClient(JiraTransport transport)
{
    public const string BoardPath = "/rest/agile/1.0/board";
    private const string ConfigurationSuffix = "configuration";
    private const string IssueSuffix = "issue";
    private const string BacklogSuffix = "backlog";
    private const string QuickFilterSuffix = "quickfilter";

    /// <summary>The most pages of quick filters read for one board.</summary>
    public const int MaxQuickFilterPages = 10;

    /// <summary>The largest <c>maxResults</c> the agile API echoes back; larger values are clamped.</summary>
    public const int MaxPageSize = 5000;

    public async Task<JiraResult<OffsetPage<BoardSummary>>> ListBoardsAsync(
        BoardListQuery query,
        int startAt,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>
        {
            $"startAt={Number(Math.Max(startAt, 0))}",
            $"maxResults={Number(Math.Clamp(query.PageSize, 1, MaxPageSize))}",
        };

        if (!string.IsNullOrWhiteSpace(query.ProjectKeyOrId))
        {
            parts.Add($"projectKeyOrId={Uri.EscapeDataString(query.ProjectKeyOrId)}");
        }

        if (query.Type is { } type && BoardMapper.TypeName(type) is { Length: > 0 } name)
        {
            parts.Add($"type={name}");
        }

        var request = JiraRequest.Get($"{BoardPath}?{string.Join('&', parts)}") with { Priority = query.Priority };
        var result = await transport.SendAsync(request, BoardJsonContext.Default.BoardList, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapList(result.Value))
            : JiraResults.Failure<OffsetPage<BoardSummary>>(result.Error);
    }

    public async Task<JiraResult<BoardSummary>> GetBoardAsync(
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var request = JiraRequest.Get($"{BoardPath}/{Number(boardId)}") with { Priority = priority };
        var result = await transport.SendAsync(request, BoardJsonContext.Default.Board, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapBoard(result.Value))
            : JiraResults.Failure<BoardSummary>(result.Error);
    }

    public async Task<JiraResult<BoardConfiguration>> GetConfigurationAsync(
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var request = JiraRequest.Get($"{BoardPath}/{Number(boardId)}/{ConfigurationSuffix}") with
        {
            Priority = priority,
        };
        var result = await transport.SendAsync(request, BoardJsonContext.Default.Configuration, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapConfiguration(result.Value))
            : JiraResults.Failure<BoardConfiguration>(result.Error);
    }

    /// <summary>
    /// The board's quick filters in board order, all pages (at most <see cref="MaxQuickFilterPages"/>). A team-managed
    /// board has none. Reads only.
    /// </summary>
    public async Task<JiraResult<IReadOnlyList<QuickFilter>>> GetQuickFiltersAsync(
        long boardId,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        var filters = new List<QuickFilter>();
        var received = 0;

        for (var page = 0; page < MaxQuickFilterPages; page++)
        {
            var url = $"{BoardPath}/{Number(boardId)}/{QuickFilterSuffix}?startAt={Number(received)}";
            var request = JiraRequest.Get(url) with { Priority = priority };
            var result = await transport.SendAsync(
                request,
                BoardJsonContext.Default.QuickFilterList,
                cancellationToken);

            if (!result.IsSuccess)
            {
                return JiraResults.Failure<IReadOnlyList<QuickFilter>>(result.Error);
            }

            var mapped = BoardMapper.MapQuickFilters(result.Value);
            var count = result.Value.Values?.Count ?? 0;
            received += count;
            filters.AddRange(mapped.Items);

            if (mapped.IsLast || count == 0)
            {
                break;
            }
        }

        filters.Sort(static (left, right) => left.Position.CompareTo(right.Position));

        return JiraResults.Success<IReadOnlyList<QuickFilter>>(filters);
    }

    /// <summary>Loads one page of everything the board shows. Pass the previous <c>Next</c>, null for first.</summary>
    public Task<JiraResult<IssuePage>> GetIssuesAsync(
        long boardId,
        BoardIssueQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken) =>
        GetIssuePageAsync($"{BoardPath}/{Number(boardId)}/{IssueSuffix}", query, cursor, cancellationToken);

    /// <summary>Loads one page of the board's backlog, as the endpoint defines it for the board kind.</summary>
    public Task<JiraResult<IssuePage>> GetBacklogAsync(
        long boardId,
        BoardIssueQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken) =>
        GetIssuePageAsync($"{BoardPath}/{Number(boardId)}/{BacklogSuffix}", query, cursor, cancellationToken);

    private async Task<JiraResult<IssuePage>> GetIssuePageAsync(
        string path,
        BoardIssueQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken)
    {
        var startAt = 0;

        if (cursor is not null && !cursor.TryGetOffset(out startAt))
        {
            return JiraResults.Failure<IssuePage>(
                new JiraError(JiraErrorKind.BadRequest, null, "The page cursor does not belong to a board."));
        }

        var pageSize = Math.Clamp(query.PageSize, 1, MaxPageSize);
        var paging = $"startAt={Number(startAt)}&maxResults={Number(pageSize)}";
        var url = $"{path}?fields={query.Extras.QueryValue(IssueFields.ListRow)}&{paging}";

        if (!string.IsNullOrWhiteSpace(query.Jql))
        {
            url += $"&jql={Uri.EscapeDataString(query.Jql)}";
        }

        var request = JiraRequest.Get(url) with { Priority = query.Priority };
        var result = await transport.SendAsync(request, BoardJsonContext.Default.IssuePage, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapIssues(result.Value, transport.Strings, query.Extras))
            : JiraResults.Failure<IssuePage>(result.Error);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
