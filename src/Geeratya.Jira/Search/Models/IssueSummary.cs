namespace Geeratya.Jira.Search.Models;

/// <summary>The list-row view of an issue: what a view shows without opening the issue.</summary>
/// <param name="Id">The numeric id.</param>
/// <param name="Key">The key, such as GCM-12.</param>
/// <param name="Summary">The title.</param>
/// <param name="Status">The status with its category.</param>
/// <param name="IssueType">The issue type.</param>
/// <param name="Priority">The priority name; null where the project has no priority field.</param>
/// <param name="Assignee">The assignee; null when unassigned.</param>
/// <param name="Labels">The labels.</param>
/// <param name="Parent">The parent stub; null when the issue has none (the field is absent in the response).</param>
/// <param name="Updated">Last update; null when Jira sent no or an unreadable timestamp.</param>
/// <param name="ProjectKey">The key of the project the issue belongs to.</param>
public sealed record IssueSummary(
    string Id,
    string Key,
    string Summary,
    IssueStatus Status,
    IssueTypeSummary IssueType,
    string? Priority,
    IssueAssignee? Assignee,
    IReadOnlyList<string> Labels,
    IssueParent? Parent,
    DateTimeOffset? Updated,
    string ProjectKey)
{
    /// <summary>The sprints the Sprint field lists, oldest first; empty when none or the field was not requested.</summary>
    public IReadOnlyList<IssueSprint> Sprints { get; init; } = [];

    /// <summary>The estimate (story points); null when the issue has none or the field was not requested.</summary>
    public double? Estimate { get; init; }

    /// <summary>The epic of the issue: its parent when that is an epic, otherwise null.</summary>
    public IssueParent? Epic => Parent is { IsEpic: true } ? Parent : null;

    /// <summary>The sprint the issue is in now (active, else future, else last closed); null for none.</summary>
    public IssueSprint? CurrentSprint => IssueSprint.Current(Sprints);
}
