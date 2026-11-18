namespace Geeratya.Core.Writes;

/// <summary>A due date set by an edit: the date it now holds (null is no due date).</summary>
public sealed record DueEdit(DateOnly? After);
