namespace Geeratya.Jira.Sprints.Models;

/// <summary>Whether a board has sprints. Team-managed boards say <c>simple</c> for both, so it takes a probe.</summary>
public enum BoardKind
{
    Kanban,
    Scrum,
}
