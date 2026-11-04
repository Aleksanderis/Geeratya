namespace Geeratya.Core.Boards;

/// <summary>Where the Done cutoff a Kanban board load applied came from, in the order they are tried.</summary>
public enum DoneCutoffSource
{
    /// <summary>The setting <c>board.done-cutoff</c>: Jira's value could not be read.</summary>
    Setting,

    /// <summary>Jira's own "Hide done work items after" (the user's view, else the published one).</summary>
    Jira,

    /// <summary>The tab's quick choice (<c>view.done-cutoff</c>), which overrides the other two for this tab.</summary>
    Choice,
}
