using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Boards;

/// <summary>The issues of one board column, in display order.</summary>
/// <param name="Name">The column name from the board configuration.</param>
/// <param name="Rows">
/// The issues whose status the column holds: in the order they were loaded, except that a sub-task follows its
/// parent when the parent is in the same column (<see cref="BoardColumns.Arrange(string, IReadOnlyList{IssueSummary})"/>).
/// </param>
/// <param name="Nested">
/// Per row, whether it is shown under its parent (the parent is an earlier row of this column). Empty means none.
/// </param>
public sealed record BoardColumnGroup(string Name, IReadOnlyList<IssueSummary> Rows, IReadOnlyList<bool>? Nested = null)
{
    /// <summary>Whether row <paramref name="index"/> sits under its parent card in this column.</summary>
    public bool IsNested(int index)
    {
        return Nested is not null && index >= 0 && index < Nested.Count && Nested[index];
    }
}
