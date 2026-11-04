namespace Geeratya.Core.Startup;

/// <summary>What a start argument asks to open: the same things as the palette's <c>:jql</c>, <c>:board</c> and so on.</summary>
public enum StartTargetKind
{
    /// <summary>An issue key: the issue detail opens in a tab.</summary>
    Issue,

    /// <summary>A board by name (columns).</summary>
    Board,

    /// <summary>A board's backlog by name.</summary>
    Backlog,

    /// <summary>A JQL query.</summary>
    Jql,

    /// <summary>A saved filter by name.</summary>
    Filter,
}
