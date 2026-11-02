namespace Geeratya.Jira.Create.Models;

/// <summary>Whether an issue type takes a parent on create, as its create metadata says.</summary>
public enum CreateParentRule
{
    /// <summary>No parent field on the screen (for example an epic).</summary>
    NotAllowed,

    Optional,

    /// <summary>Sub-task types: Jira refuses the create without a parent.</summary>
    Required,
}
