using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Boards;

/// <summary>Maps the wire shapes of the board endpoints. Pure and tolerant: a missing field is neutral.</summary>
internal static class BoardMapper
{
    private const string RankFieldPrefix = "customfield_";

    public static OffsetPage<BoardSummary> MapList(BoardWire.BoardList wire)
    {
        var values = wire.Values ?? [];
        var items = new List<BoardSummary>(values.Count);

        foreach (var board in values)
        {
            items.Add(MapBoard(board));
        }

        var startAt = wire.StartAt ?? 0;
        var isLast = IsLast(startAt, values.Count, wire.Total, wire.IsLast);

        return new OffsetPage<BoardSummary>(items, startAt, wire.MaxResults ?? values.Count, wire.Total, isLast);
    }

    public static BoardSummary MapBoard(BoardWire.Board board) =>
        new(
            board.Id ?? 0,
            board.Name ?? string.Empty,
            ParseType(board.Type),
            board.Location?.ProjectKey ?? string.Empty);

    /// <summary>
    /// Only a custom field is a story-point estimate; a board estimating by "Original time estimate" names a system
    /// field in seconds, which is not shown as points.
    /// </summary>
    private static string? CustomFieldOrNull(string? fieldId) =>
        fieldId is not null && fieldId.StartsWith(RankFieldPrefix, StringComparison.Ordinal) ? fieldId : null;

    public static BoardConfiguration MapConfiguration(BoardWire.Configuration wire)
    {
        var columns = new List<BoardColumn>();

        foreach (var column in wire.ColumnConfig?.Columns ?? [])
        {
            var ids = (column.Statuses ?? []).Select(status => status.Id).OfType<string>().ToList();
            columns.Add(new BoardColumn(column.Name ?? string.Empty, ids));
        }

        var query = wire.SubQuery?.Query;

        return new BoardConfiguration(
            wire.Id ?? 0,
            wire.Name ?? string.Empty,
            ParseType(wire.Type),
            columns,
            wire.Filter?.Id ?? string.Empty,
            string.IsNullOrWhiteSpace(query) ? null : query,
            wire.Ranking?.RankCustomFieldId is { } rank ? $"{RankFieldPrefix}{rank}" : null,
            wire.ColumnConfig?.ConstraintType)
        {
            EstimationRead = true,
            EstimationFieldId = CustomFieldOrNull(wire.Estimation?.Field?.FieldId),
            EstimationFieldName = string.IsNullOrWhiteSpace(wire.Estimation?.Field?.DisplayName)
                ? null
                : wire.Estimation.Field.DisplayName.Trim(),
        };
    }

    /// <summary>The agile API has no <c>isLast</c>: the page ends at <c>total</c> or when empty.</summary>
    public static IssuePage MapIssues(BoardWire.IssuePage wire, IssueExtraFields? extras = null)
    {
        var items = IssueSummaryMapper.MapIssues(wire.Issues, extras);
        var startAt = wire.StartAt ?? 0;
        var isLast = IsLast(startAt, items.Count, wire.Total, null);
        var next = isLast ? null : PageCursor.Offset(startAt + items.Count);

        return new IssuePage(items, next, wire.Total);
    }

    public static BoardType ParseType(string? type) =>
        type?.ToLowerInvariant() switch
        {
            "kanban" => BoardType.Kanban,
            "scrum" => BoardType.Scrum,
            "simple" => BoardType.Simple,
            _ => BoardType.Unknown,
        };

    public static string TypeName(BoardType type) =>
        type switch
        {
            BoardType.Kanban => "kanban",
            BoardType.Scrum => "scrum",
            BoardType.Simple => "simple",
            _ => string.Empty,
        };

    internal static bool IsLast(int startAt, int count, int? total, bool? isLast)
    {
        if (count == 0 || isLast == true)
        {
            return true;
        }

        return total is { } known ? startAt + count >= known : isLast ?? false;
    }
}
