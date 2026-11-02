using Geeratya.Core.Writes;

namespace Geeratya.Core.Forms;

/// <summary>
/// What a form is for: which metadata it loads and which write its fields become. Edit is built; the transition and
/// create targets are added by the issues that open those forms (#159, #162), each a subclass here, so the form
/// itself never learns about them.
/// </summary>
public abstract record FormTarget
{
    /// <summary>The heading of the form: <c>Edit GCM-3</c>.</summary>
    public abstract string Title { get; }

    /// <summary>The issue the form is about, or null for a form with no issue yet (create).</summary>
    public abstract string? IssueKey { get; }

    /// <summary>The write for the changed fields of a valid form.</summary>
    public abstract WriteRequest Request(IReadOnlyList<FieldChange> changes);
}
