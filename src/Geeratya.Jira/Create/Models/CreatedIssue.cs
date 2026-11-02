namespace Geeratya.Jira.Create.Models;

/// <summary>The issue Jira created: the key is what the user sees, the id is Jira's own.</summary>
public sealed record CreatedIssue(string Id, string Key);
