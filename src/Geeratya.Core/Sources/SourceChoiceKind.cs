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
}
