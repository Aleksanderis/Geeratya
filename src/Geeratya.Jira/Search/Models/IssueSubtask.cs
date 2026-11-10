namespace Geeratya.Jira.Search.Models;

/// <summary>
/// A sub-task as the stub in its parent's <c>subtasks</c> field: key, title, status and type name. The stub has no
/// assignee: <see cref="AssigneeKnown"/> turns true once a <c>parent = KEY</c> search has filled it in.
/// </summary>
/// <param name="Key">The sub-task key.</param>
/// <param name="Summary">The title.</param>
/// <param name="Status">The status with its category.</param>
/// <param name="TypeName">The issue type's name, empty when Jira sent none.</param>
public sealed record IssueSubtask(string Key, string Summary, IssueStatus Status, string TypeName)
{
    /// <summary>Whether the status is in the done category.</summary>
    public bool IsDone => Status.CategoryKey == StatusCategoryKeys.Done;

    /// <summary>The assignee; null when unassigned or not yet known (see <see cref="AssigneeKnown"/>).</summary>
    public IssueAssignee? Assignee { get; init; }

    /// <summary>Whether <see cref="Assignee"/> was read from Jira (the stub itself never carries it).</summary>
    public bool AssigneeKnown { get; init; }
}
