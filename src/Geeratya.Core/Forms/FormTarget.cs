using Geeratya.Core.Writes;

namespace Geeratya.Core.Forms;

/// <summary>
/// What a form is for: which metadata it loads and which write its fields become. Edit and create are built; the
/// transition target is added by the issue that opens that form (#159), a subclass here, so the form itself never
/// learns about it.
/// </summary>
public abstract record FormTarget
{
    /// <summary>The heading of the form: <c>Edit GCM-3</c>.</summary>
    public abstract string Title { get; }

    /// <summary>The issue the form is about, or null for a form with no issue yet (create).</summary>
    public abstract string? IssueKey { get; }

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

    /// <summary>A reason the loaded form can never be sent, shown on its top line, or null.</summary>
    public virtual string? Blocked(IReadOnlyList<FormField> fields)
    {
        return null;
    }
}
