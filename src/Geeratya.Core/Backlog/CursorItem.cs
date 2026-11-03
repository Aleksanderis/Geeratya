namespace Geeratya.Core.Backlog;

/// <summary>
/// One place the keyboard cursor of a Scrum backlog visits: a row, or the header of an empty sprint
/// (<paramref name="SprintId"/> set; <paramref name="Row"/> is then the row that follows the section).
/// </summary>
public readonly record struct CursorItem(int Row, long? SprintId);
