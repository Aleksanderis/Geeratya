namespace Geeratya.Jira.Search.Models;

/// <summary>An issue's type. Hierarchy level and sub-task flag decide the role, never the name.</summary>
public sealed record IssueTypeSummary(string Id, string Name, int HierarchyLevel, bool IsSubtask);
