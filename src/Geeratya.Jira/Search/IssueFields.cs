namespace Geeratya.Jira.Search;

/// <summary>
/// The field ids of the list-row field list. Never <c>*navigable</c> or <c>*all</c>: ask for what is shown.
/// </summary>
public static class IssueFields
{
    /// <summary>The JSON property that holds the fields of an issue, a stub or the requested field list.</summary>
    public const string FieldsProperty = "fields";

    public const string Summary = "summary";
    public const string Status = "status";
    public const string IssueType = "issuetype";
    public const string Priority = "priority";
    public const string Assignee = "assignee";
    public const string Labels = "labels";
    public const string Parent = "parent";
    public const string Updated = "updated";
    public const string Project = "project";

    /// <summary>The fields every list row needs, in the order they are sent.</summary>
    public static readonly IReadOnlyList<string> ListRow =
    [
        Summary,
        Status,
        IssueType,
        Priority,
        Assignee,
        Labels,
        Parent,
        Updated,
        Project,
    ];

    /// <summary>The same list as one comma-separated query value.</summary>
    public static readonly string ListRowQueryValue = string.Join(',', ListRow);
}
