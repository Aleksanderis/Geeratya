namespace Geeratya.Jira.Sprints.Models;

/// <summary>
/// A sprint update: a null member is left as it is. An empty <see cref="Goal"/> clears the goal. A date cannot be
/// cleared by the partial update (<c>null</c> is ignored), so <see cref="ClearStartDate"/> and
/// <see cref="ClearEndDate"/> ask for a replacement of the whole sprint instead
/// (<see cref="SprintWriteClient.ReplaceAsync"/>); <see cref="StartDate"/> and <see cref="EndDate"/> are then the dates
/// to keep.
/// </summary>
public sealed record SprintEdit(
    string? Name,
    string? Goal,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    bool ClearStartDate = false,
    bool ClearEndDate = false)
{
    public bool IsEmpty =>
        Name is null && Goal is null && StartDate is null && EndDate is null && !ClearsDate;

    /// <summary>The edit removes a date, which only a replacement of the sprint can do.</summary>
    public bool ClearsDate => ClearStartDate || ClearEndDate;
}
