namespace Geeratya.Jira.Suggestions.Models;

/// <summary>
/// One candidate for a picker: what to write (<see cref="Id"/>) and what to show (<see cref="Label"/>).
/// </summary>
/// <param name="Id">Account id (users), label text, issue key, or component or version id.</param>
/// <param name="Label">Display name, label text, issue summary, or component or version name.</param>
/// <param name="Detail">A second column: the issue key for issues; null otherwise.</param>
/// <param name="IsInactive">A deactivated user or an archived version: still listed, marked by the UI.</param>
/// <param name="Kind">The issue type name for issues (Epic, Story, Sub-task, ...); null when Jira did not say.</param>
/// <param name="HierarchyLevel">The issue type's hierarchy level for issues (epic 1, standard 0, sub-task -1); null when Jira did not say.</param>
public sealed record Suggestion(string Id, string Label, string? Detail, bool IsInactive, string? Kind = null,
    int? HierarchyLevel = null);
