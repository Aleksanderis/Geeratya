namespace Geeratya.Jira.Sprints.Models;

/// <summary>
/// A sprint. <c>Goal</c> is null when none is set; <c>StartDate</c> and <c>EndDate</c> are null for a sprint never
/// given dates, <c>CompleteDate</c> is set for closed sprints only. <c>OriginBoardId</c> is 0 when Jira omits it.
/// </summary>
public sealed record Sprint(
    long Id,
    string Name,
    SprintState State,
    string? Goal,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    DateTimeOffset? CompleteDate,
    long OriginBoardId);
