namespace Geeratya.Core.Backlog;

/// <summary>A display line of the backlog: its section and, for a row line, the row index.</summary>
/// <param name="Section">The section the line belongs to.</param>
/// <param name="Row">The row index; null for a header, goal or empty note line.</param>
public readonly record struct BacklogLine(BacklogSection Section, int? Row);
