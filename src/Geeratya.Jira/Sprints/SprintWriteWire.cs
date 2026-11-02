namespace Geeratya.Jira.Sprints;

/// <summary>The JSON shape of a sprint in a write reply. Every member is optional.</summary>
internal static class SprintWriteWire
{
    internal sealed record Sprint(
        long? Id,
        string? Name,
        string? State,
        long? OriginBoardId,
        string? Goal,
        string? StartDate,
        string? EndDate,
        string? CompleteDate);
}
