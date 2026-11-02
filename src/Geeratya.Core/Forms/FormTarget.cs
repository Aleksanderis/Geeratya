using Geeratya.Core.Writes;

namespace Geeratya.Core.Forms;

/// <summary>
/// What a form is for: which metadata it loads and which write its fields become. Edit and transition are built; the
/// create target is added by the issue that opens that form (#162), a subclass here, so the form itself never learns
/// about it.
/// </summary>
public abstract record FormTarget
{
    /// <summary>The heading of the form: <c>Edit GCM-3</c>.</summary>
    public abstract string Title { get; }

    /// <summary>The issue the form is about, or null for a form with no issue yet (create).</summary>
    public abstract string? IssueKey { get; }

    /// <summary>The message when the metadata holds nothing to fill in.</summary>
    public virtual string EmptyText => "Nothing here can be edited";

    /// <summary>A submit with no changed field is a valid write (a transition with optional fields).</summary>
    public virtual bool AllowsNoChanges => false;

    /// <summary>The form closes as soon as its write starts (a picker); the result goes to the status line.</summary>
    public virtual bool ClosesOnSend => false;

    /// <summary>False when the form stays invisible while it loads (a card move that may send at once).</summary>
    public virtual bool ShowsWhileLoading => true;

    /// <summary>
    /// The changes to submit as soon as the metadata has loaded, without showing the form (a card move with only one
    /// way to go), or null to show it.
    /// </summary>
    public virtual IReadOnlyList<FieldChange>? Automatic => null;

    /// <summary>
    /// The form to open instead of writing when <paramref name="changes"/> are submitted (the picked transition
    /// asks for fields), or null to write.
    /// </summary>
    public virtual FormTarget? Following(IReadOnlyList<FieldChange> changes)
    {
        return null;
    }

    /// <summary>The write for the changed fields of a valid form.</summary>
    public abstract WriteRequest Request(IReadOnlyList<FieldChange> changes);

    /// <summary>
    /// The target once its fields are known: a target may adapt to the metadata (a create drops the parent when the
    /// issue type takes none). The default is the target itself.
    /// </summary>
    public virtual FormTarget Loaded(IReadOnlyList<FormField> fields)
    {
        return this;
    }
}
