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

    /// <summary>The largest <c>maxResults</c> the agile API echoes back; larger values are clamped to it (probed).</summary>
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

    public async Task<JiraResult<BoardSummary>> GetBoardAsync(long boardId, CancellationToken cancellationToken)
    {
        var request = JiraRequest.Get($"{BoardPath}/{Number(boardId)}");
        var result = await transport.SendAsync(request, BoardJsonContext.Default.Board, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapBoard(result.Value))
            : JiraResults.Failure<BoardSummary>(result.Error);
    }

    public async Task<JiraResult<BoardConfiguration>> GetConfigurationAsync(
        long boardId,
        CancellationToken cancellationToken)
    {
        var request = JiraRequest.Get($"{BoardPath}/{Number(boardId)}/configuration");
        var result = await transport.SendAsync(request, BoardJsonContext.Default.Configuration, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapConfiguration(result.Value))
            : JiraResults.Failure<BoardConfiguration>(result.Error);
    }

    /// <summary>Loads one page of everything the board shows. Pass the previous page's <c>Next</c>, null for the first.</summary>
    public Task<JiraResult<IssuePage>> GetIssuesAsync(
        long boardId,
        BoardIssueQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken) =>
        GetIssuePageAsync($"{BoardPath}/{Number(boardId)}/issue", query, cursor, cancellationToken);

    /// <summary>Loads one page of the board's backlog, as the endpoint defines it for the board kind.</summary>
    public Task<JiraResult<IssuePage>> GetBacklogAsync(
        long boardId,
        BoardIssueQuery query,
        PageCursor? cursor,
        CancellationToken cancellationToken) =>
        GetIssuePageAsync($"{BoardPath}/{Number(boardId)}/backlog", query, cursor, cancellationToken);

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
        var url = $"{path}?fields={IssueFields.ListRowQueryValue}&startAt={Number(startAt)}&maxResults={Number(pageSize)}";

        if (!string.IsNullOrWhiteSpace(query.Jql))
        {
            url += $"&jql={Uri.EscapeDataString(query.Jql)}";
        }

        var request = JiraRequest.Get(url) with { Priority = query.Priority };
        var result = await transport.SendAsync(request, BoardJsonContext.Default.IssuePage, cancellationToken);

        return result.IsSuccess
            ? JiraResults.Success(BoardMapper.MapIssues(result.Value))
            : JiraResults.Failure<IssuePage>(result.Error);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
