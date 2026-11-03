namespace Geeratya.Core.Sources;

/// <summary>What a picker row opens.</summary>
public enum SourceChoiceKind
{
    Board,
    Backlog,
    Filter,
    Query,

    /// <summary>A JQL string opened before (history).</summary>
    Recent,

    /// <summary>A sprint of the board (<see cref="SourceChoice.Sprint"/>).</summary>
    Sprint,

    /// <summary>The row that lists more closed sprints; it opens nothing.</summary>
    MoreClosed,
}
