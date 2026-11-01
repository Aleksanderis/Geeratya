namespace Geeratya.Jira.Search.Models;

/// <summary>The assignee of an issue; the display name can be that of a deleted or anonymised account.</summary>
public sealed record IssueAssignee(string AccountId, string DisplayName);
