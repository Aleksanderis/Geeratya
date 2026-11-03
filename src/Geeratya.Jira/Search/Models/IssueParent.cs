namespace Geeratya.Jira.Search.Models;

/// <summary>The parent of an issue (epic, or parent of a sub-task) as the stub Jira embeds in the child.</summary>
/// <param name="Key">The parent key.</param>
/// <param name="Summary">The parent title; null when Jira sent none.</param>
/// <param name="IsEpic">Whether the parent's issue type is on the epic hierarchy level (read from Jira, not by name).</param>
public sealed record IssueParent(string Key, string? Summary, bool IsEpic = false);
