namespace Geeratya.Jira.Search.Models;

/// <summary>
/// A sub-task as the stub in its parent's <c>subtasks</c> field: key, title, status and type name. The stub has no
/// assignee, so a list of sub-tasks built from it shows none.
/// </summary>
/// <param name="Key">The sub-task key.</param>
/// <param name="Summary">The title.</param>
/// <param name="Status">The status with its category.</param>
/// <param name="TypeName">The issue type's name, empty when Jira sent none.</param>
public sealed record IssueSubtask(string Key, string Summary, IssueStatus Status, string TypeName)
{
    /// <summary>Whether the status is in the done category.</summary>
    public bool IsDone => Status.CategoryKey == StatusCategoryKeys.Done;
}
