using Geeratya.Jira.Search;

namespace Geeratya.Jira.Boards;

/// <summary>The JSON shapes of the agile board endpoints as Jira sends them. Every member is optional.</summary>
internal static class BoardWire
{
    internal sealed record BoardList(List<Board>? Values, int? StartAt, int? MaxResults, int? Total, bool? IsLast);

    internal sealed record Board(long? Id, string? Name, string? Type, Location? Location);

    internal sealed record Location(string? ProjectKey);

    internal sealed record Configuration(
        long? Id,
        string? Name,
        string? Type,
        ColumnConfig? ColumnConfig,
        FilterRef? Filter,
        SubQuery? SubQuery,
        Ranking? Ranking);

    internal sealed record ColumnConfig(List<Column>? Columns, string? ConstraintType);

    internal sealed record Column(string? Name, List<StatusRef>? Statuses);

    internal sealed record StatusRef(string? Id);

    internal sealed record FilterRef(string? Id);

    internal sealed record SubQuery(string? Query);

    internal sealed record Ranking(long? RankCustomFieldId);

    /// <summary>Board issues and backlog: a page of issues with <c>startAt</c>/<c>total</c>, no <c>isLast</c>.</summary>
    internal sealed record IssuePage(List<SearchWire.Issue>? Issues, int? StartAt, int? MaxResults, int? Total);
}
