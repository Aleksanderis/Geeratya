namespace Geeratya.Jira.Edit.Models;

/// <summary>
/// How the app edits a field, decided from Jira's schema type (never from a field id). A field of a type the app
/// cannot write is <see cref="Unsupported"/>: show it read-only with a link to the browser, never drop it.
/// </summary>
public enum EditFieldKind
{
    Unsupported,
    Text,
    RichText,
    Number,
    Date,
    DateTime,
    Option,
    MultiOption,
    User,
    Labels,
    Parent,
}
