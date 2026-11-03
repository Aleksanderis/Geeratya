namespace Geeratya.Core.Views;

/// <summary>Facts about source kinds that more than one place needs.</summary>
public static class ViewSourceKinds
{
    /// <summary>
    /// The source loads through the agile board endpoints (offset paging, every page walked), is ordered by rank and
    /// carries the board configuration: board, backlog, sprint and Scrum backlog.
    /// </summary>
    public static bool IsBoardBased(this ViewSourceKind source)
    {
        return source is ViewSourceKind.Board
            or ViewSourceKind.Backlog
            or ViewSourceKind.Sprint
            or ViewSourceKind.ScrumBacklog;
    }
}
