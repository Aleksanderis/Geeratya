namespace Geeratya.Jira.Boards.Models;

/// <summary>A quick filter of a board: a named JQL clause the board offers on top of its own filter.</summary>
/// <param name="Id">The filter's id.</param>
/// <param name="Name">The name shown on the board.</param>
/// <param name="Jql">The clause; applied together with the board's issues.</param>
/// <param name="Position">The order on the board.</param>
public sealed record QuickFilter(long Id, string Name, string Jql, int Position);
