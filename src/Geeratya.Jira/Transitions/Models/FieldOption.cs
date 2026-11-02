namespace Geeratya.Jira.Transitions.Models;

/// <summary>One allowed value of a field: the id Jira expects back and the label to show.</summary>
public sealed record FieldOption(string Id, string Label);
