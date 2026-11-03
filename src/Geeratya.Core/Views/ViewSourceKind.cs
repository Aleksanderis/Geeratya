namespace Geeratya.Core.Views;

public enum ViewSourceKind
{
    Jql,
    SavedFilter,
    Board,
    Backlog,

    /// <summary>One sprint (or the active ones) of a Scrum board; the source value is a <see cref="SprintSource"/>.</summary>
    Sprint,

    /// <summary>Everything on a Scrum board, sprints and backlog; the source value is the board id.</summary>
    ScrumBacklog,
}
