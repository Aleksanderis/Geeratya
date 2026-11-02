namespace Geeratya.Jira.Writes.Models;

/// <summary>One field Jira refused, by field id (<c>summary</c>, <c>customfield_10010</c>), with its message.</summary>
public sealed record WriteFieldError(string FieldId, string Message);
