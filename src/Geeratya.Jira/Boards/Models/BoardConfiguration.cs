namespace Geeratya.Jira.Boards.Models;

/// <summary>
/// What a board shows. The JQL behind it is the saved filter <c>FilterId</c> plus <c>SubQuery</c> when present.
/// </summary>
/// <param name="BoardId">The board's id.</param>
/// <param name="Name">The board's name.</param>
/// <param name="Type">The board kind.</param>
/// <param name="Columns">The columns in board order. The first column is not necessarily the backlog.</param>
/// <param name="FilterId">The id of the board's saved filter; empty if Jira sent none.</param>
/// <param name="SubQuery">An extra JQL clause of the board; null when the board has none.</param>
/// <param name="RankFieldId">The rank field as a field id such as <c>customfield_10019</c>; null if absent.</param>
/// <param name="ConstraintType">The column constraint (<c>issueCount</c>, <c>none</c>, ...); null if absent.</param>
/// <remarks>Scrum boards carry <see cref="EstimationFieldId"/>; Kanban boards have no estimation.</remarks>
public sealed record BoardConfiguration(
    long BoardId,
    string Name,
    BoardType Type,
    IReadOnlyList<BoardColumn> Columns,
    string FilterId,
    string? SubQuery,
    string? RankFieldId,
    string? ConstraintType)
{
    /// <summary>The estimation field (story points) as a field id; null for a board without field estimation.</summary>
    public string? EstimationFieldId { get; init; }

    /// <summary>The estimation field's name on the board (for example "Story Points"); null when Jira sent none.</summary>
    public string? EstimationFieldName { get; init; }
}
