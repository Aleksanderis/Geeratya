namespace Geeratya.Jira.Boards.Models;

/// <summary>The kind of a board as Jira names it: <c>kanban</c>, <c>scrum</c>, <c>simple</c> (team-managed).</summary>
public enum BoardType
{
    Unknown,
    Kanban,
    Scrum,
    Simple,
}
