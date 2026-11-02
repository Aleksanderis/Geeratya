using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// What to write, with every id already resolved. Pure data, so a test can build one. Each kind of write adds a
/// subclass that knows its keys, its optimistic row change and the texts the status line uses.
/// </summary>
public abstract record WriteRequest
{
    public abstract OperationKind Kind { get; }

    /// <summary>The issues the write changes; a second write on any of them is refused while this one runs.</summary>
    public abstract IReadOnlyList<string> Keys { get; }

    /// <summary>
    /// True for a write that deletes its issues: no optimistic change, the rows and open details of the issues go
    /// when Jira confirmed, and the read after the write is not made (the issues are gone).
    /// </summary>
    public virtual bool RemovesIssues => false;

    /// <summary>The activity for the busy refusal: <c>GCM-3 is busy (assigning)</c>.</summary>
    public abstract string BusyText { get; }

    /// <summary>The status line when the write starts.</summary>
    public abstract string StartedText { get; }

    /// <summary>The status line when Jira confirmed the write.</summary>
    public abstract string DoneText { get; }

    /// <summary>The status line when the reply was lost and the issue is being checked.</summary>
    public abstract string CheckingText { get; }

    /// <summary>The status line when the check found that the write did not happen.</summary>
    public abstract string NotAppliedText { get; }

    /// <summary>What was attempted, for failures: <c>No permission to assign GCM-3</c>.</summary>
    public abstract string ActionText { get; }

    /// <summary>
    /// The optimistic change for a row that shows this issue, or null when the row shows nothing of the write (or
    /// the write is not optimistic).
    /// </summary>
    public abstract RowChange? Plan(IssueSummary row);

    /// <summary>
    /// True when <paramref name="row"/>, read from Jira after the write, shows the intended change. Used to check a
    /// lost reply, and to not trust a read that still lags behind the write (the search index is not instant).
    /// </summary>
    public abstract bool IsShownBy(IssueSummary row);
}
