using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// What an optimistic update changed in the rows of one issue, kept so a failed write can undo exactly that. Rows are
/// immutable, so a change builds a new row.
/// </summary>
/// <param name="Key">The issue the change applies to.</param>
public abstract record RowChange(string Key)
{
    /// <summary>The row with the change applied.</summary>
    public abstract IssueSummary Patch(IssueSummary row);

    /// <summary>
    /// The row with the change undone, but only where the field still holds the patched value: a newer value (from
    /// a reload) wins and the row is returned as it is.
    /// </summary>
    public abstract IssueSummary Revert(IssueSummary row);
}
