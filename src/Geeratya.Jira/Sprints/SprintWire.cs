namespace Geeratya.Jira.Sprints;

/// <summary>The JSON shapes of the sprint endpoints as Jira sends them. Every member is optional.</summary>
internal static class SprintWire
{
    internal sealed record SprintList(List<Sprint>? Values, int? StartAt, int? MaxResults, int? Total, bool? IsLast);

    internal sealed record Sprint(
        long? Id,
        string? Name,
        string? State,
        string? Goal,
        long? OriginBoardId,
        string? StartDate,
        string? EndDate,
        string? CompleteDate);
}
