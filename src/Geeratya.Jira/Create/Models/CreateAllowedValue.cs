namespace Geeratya.Jira.Create.Models;

/// <summary>One choice of a select-like create field: the id Jira wants back and the label to show.</summary>
public sealed record CreateAllowedValue(string Id, string Label);
