namespace Geeratya.Jira.Create;

/// <summary>Ids of the system fields the create call sets itself; the rest comes from the create metadata.</summary>
public static class CreateFieldIds
{
    public const string Project = "project";
    public const string IssueType = "issuetype";
    public const string Summary = "summary";
    public const string Description = "description";
    public const string Parent = "parent";
}
