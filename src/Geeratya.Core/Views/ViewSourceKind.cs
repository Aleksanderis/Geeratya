namespace Geeratya.Core.Views;

public enum ViewSourceKind
{
    Jql,
    SavedFilter,
    Board,
    Backlog,

    /// <summary>One sprint (or the active ones) of a Scrum board; the value is a <see cref="SprintSource"/>.</summary>
    Sprint,

    /// <summary>Open and future sprints and the backlog of a Scrum board; the source value is the board id.</summary>
    ScrumBacklog,
}
