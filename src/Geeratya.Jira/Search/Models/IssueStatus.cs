namespace Geeratya.Jira.Search.Models;

/// <summary>
/// An issue's status. The category key (<c>new</c>, <c>indeterminate</c>, <c>done</c>) comes from Jira, never from
/// the name.
/// </summary>
public sealed record IssueStatus(string Id, string Name, string CategoryKey);
