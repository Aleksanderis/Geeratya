namespace Geeratya.Jira.Sprints.Models;

/// <summary>The sprint Jira answered a sprint write with. Dates are absent for a sprint never given dates.</summary>
public sealed record WrittenSprint(
    long Id,
    string Name,
    WrittenSprintState State,
    long? OriginBoardId,
    string? Goal,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset? CompleteDate);
