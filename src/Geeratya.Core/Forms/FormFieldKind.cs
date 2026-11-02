namespace Geeratya.Core.Forms;

/// <summary>
/// How the form edits a field, decided from Jira's schema type and never from a field id. A kind the form cannot edit
/// is shown read-only with a link to the browser, never dropped.
/// </summary>
public enum FormFieldKind
{
    /// <summary>A type the form cannot write.</summary>
    Unsupported,

    /// <summary>Single-line text.</summary>
    Text,

    /// <summary>Multi-line text (rich text, ADF in API v3).</summary>
    Paragraph,

    Number,

    /// <summary>A day, typed as <c>yyyy-MM-dd</c>.</summary>
    Date,

    /// <summary>A moment, typed as <c>yyyy-MM-dd HH:mm</c> in the local time zone.</summary>
    DateTime,

    /// <summary>One of the allowed values (priority, resolution, select list, one version).</summary>
    Select,

    /// <summary>Several of the allowed values (components, versions, checkboxes).</summary>
    MultiSelect,

    /// <summary>Free-form labels.</summary>
    Labels,

    /// <summary>A user; searching for one arrives with the suggestion popup, so it is read-only for now.</summary>
    User,

    /// <summary>The parent or epic; searching for one arrives with the suggestion popup, so it is read-only for now.</summary>
    Parent,
}
