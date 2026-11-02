namespace Geeratya.Core.Forms;

/// <summary>
/// The Jira system fields a list row shows, so an edit can patch the row at once. System ids are the same on every
/// site (custom ids never are and are not named here).
/// </summary>
public static class FormFieldIds
{
    public const string Summary = "summary";
    public const string Labels = "labels";
    public const string Description = "description";
    public const string Parent = "parent";
    public const string Priority = "priority";
    public const string Assignee = "assignee";
}
