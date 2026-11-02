namespace Geeratya.Jira.Create.Models;

/// <summary>How a create field is edited and written, derived from its schema.</summary>
public enum CreateFieldKind
{
    /// <summary>Single-line text, sent as a string.</summary>
    Text,

    /// <summary>Multi-line text; Atlassian Document Format in API v3.</summary>
    RichText,

    /// <summary>One value out of <see cref="CreateField.AllowedValues"/> (priority, select).</summary>
    SingleChoice,

    /// <summary>Several values out of <see cref="CreateField.AllowedValues"/> (components, versions).</summary>
    MultiChoice,

    /// <summary>Free-form labels.</summary>
    Labels,

    /// <summary>A user, written as an account id found by the user search.</summary>
    User,

    /// <summary>A day, sent as <c>yyyy-MM-dd</c>.</summary>
    Date,

    /// <summary>A moment, sent with its offset as Jira reads it.</summary>
    DateTime,

    Number,

    /// <summary>The parent issue; see <see cref="CreateParentRule"/>.</summary>
    Parent,

    /// <summary>Set by the app from its selection (project, issue type).</summary>
    Fixed,

    /// <summary>A type the app cannot write; shown read-only with a browser link, never dropped.</summary>
    Unsupported,
}
