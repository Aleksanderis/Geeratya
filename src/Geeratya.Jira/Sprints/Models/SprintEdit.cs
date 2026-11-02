namespace Geeratya.Jira.Sprints.Models;

/// <summary>
/// A partial sprint update: a null member is left as it is. An empty <see cref="Goal"/> clears the goal.
/// </summary>
public sealed record SprintEdit(string? Name, string? Goal, DateTimeOffset? StartDate, DateTimeOffset? EndDate)
{
    public bool IsEmpty => Name is null && Goal is null && StartDate is null && EndDate is null;
}
