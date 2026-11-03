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

    /// <summary>Extra field ids the read-back must request so <see cref="IsShownBy"/> can see the change.</summary>
    public virtual IReadOnlyList<string> ReadBackFieldIds => [];

    /// <summary>
    /// Whether the write changes what the list rows and the issue header show. False for a write that only touches
    /// the issue's comments: no row is marked stale, no search is read and the issue is not reloaded.
    /// </summary>
    public virtual bool ChangesRows => true;

    /// <summary>
    /// True for a write that deletes its issues: no optimistic change, the rows and open details of the issues go
    /// when Jira confirmed, and the read after the write is not made (the issues are gone).
    /// </summary>
    public virtual bool RemovesIssues => false;

    /// <summary>
    /// True for a write that creates an issue: it has no key at the start, the key arrives with the reply, and the
    /// origin tab reloads when Jira confirmed.
    /// </summary>
    public virtual bool CreatesIssue => false;

    /// <summary>The status line when the check of a lost reply could not decide what happened.</summary>
    public virtual string UnreachableText =>
        $"{string.Join(", ", Keys)}: could not check the result; reload to see the current state";

    /// <summary>The activity for the busy refusal: <c>GCM-3 is busy (assigning)</c>.</summary>
    public abstract string BusyText { get; }

    /// <summary>The status line when the write starts.</summary>
    public abstract string StartedText { get; }

    /// <summary>The status line when Jira confirmed the write.</summary>
    public abstract string DoneText { get; }

    /// <summary>
    /// The status line when a lost reply was resolved to done by the check; defaults to <see cref="DoneText"/>.
    /// A write whose check cannot tell "done" from another cause says so here.
    /// </summary>
    public virtual string VerifiedDoneText => DoneText;

    /// <summary>The status line when the reply was lost and the issue is being checked.</summary>
    public abstract string CheckingText { get; }

    /// <summary>The status line when the check found that the write did not happen.</summary>
    public abstract string NotAppliedText { get; }

    /// <summary>What was attempted, for failures: <c>No permission to assign GCM-3</c>.</summary>
    public abstract string ActionText { get; }

    /// <summary>
    /// The question to ask before the write starts (what changes, and that Jira cannot undo it), or null when the
    /// write starts at once. The answer <c>y</c> runs the request again with <c>Confirmed</c> set.
    /// </summary>
    public virtual string? AskText => null;

    /// <summary>The name of the <c>y</c> answer in the Confirm dialog: says what the write does ("Move issue").</summary>
    public virtual string AskLabel => "Confirm";

    /// <summary>
    /// The title chip of the Confirm dialog: the action and its target ("Move GCM-8"), or null for "Confirm".
    /// </summary>
    public virtual string? AskHeading => null;

    /// <summary>
    /// True when the question is about something hard to undo (side effects, many issues): the bad frame colour.
    /// </summary>
    public virtual bool AskSevere => false;

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

    /// <summary>
    /// Whether the issues are read again after the write (one search): false when a row shows nothing of the write
    /// (a rank), so a re-read would prove nothing and is not made.
    /// </summary>
    public virtual bool ReadsBack => true;
}
