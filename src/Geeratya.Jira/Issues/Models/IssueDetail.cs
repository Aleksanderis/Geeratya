using Geeratya.Jira.Adf;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Issues.Models;

/// <summary>The opened issue: list-row fields plus reporter, description, parent, sub-tasks, links, attachments.</summary>
/// <param name="Id">The numeric id.</param>
/// <param name="Key">The key, such as GCM-12.</param>
/// <param name="Summary">The title.</param>
/// <param name="Status">The status with its category.</param>
/// <param name="IssueType">The issue type.</param>
/// <param name="Priority">The priority name; null where the project has no priority field.</param>
/// <param name="Assignee">The assignee; null when unassigned.</param>
/// <param name="Reporter">The reporter; null for a missing or deleted account.</param>
/// <param name="Labels">The labels.</param>
/// <param name="Created">Creation time; null when unreadable.</param>
/// <param name="Updated">Last update; null when unreadable.</param>
/// <param name="ProjectKey">The key of the project the issue belongs to.</param>
/// <param name="Description">The description; null when empty.</param>
/// <param name="Parent">The parent (epic, or parent of a sub-task); null when there is none.</param>
/// <param name="Subtasks">The sub-tasks.</param>
/// <param name="Links">The issue links.</param>
/// <param name="Attachments">The attachments' metadata.</param>
public sealed record IssueDetail(
    string Id,
    string Key,
    string Summary,
    IssueStatus Status,
    IssueTypeSummary IssueType,
    string? Priority,
    IssueAssignee? Assignee,
    IssueAssignee? Reporter,
    IReadOnlyList<string> Labels,
    DateTimeOffset? Created,
    DateTimeOffset? Updated,
    string ProjectKey,
    AdfDocument? Description,
    IssueStub? Parent,
    IReadOnlyList<IssueStub> Subtasks,
    IReadOnlyList<IssueLink> Links,
    IReadOnlyList<IssueAttachment> Attachments);
