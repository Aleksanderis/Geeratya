namespace Geeratya.Jira.Search.Models;

/// <summary>The parent of an issue (epic, or parent of a sub-task) as the stub Jira embeds in the child.</summary>
public sealed record IssueParent(string Key, string? Summary);
