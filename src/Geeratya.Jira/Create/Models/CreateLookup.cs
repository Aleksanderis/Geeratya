namespace Geeratya.Jira.Create.Models;

/// <summary>The project list a create field's values come from, when it has one to search.</summary>
public enum CreateLookup
{
    /// <summary>No project list: the allowed values are all there is.</summary>
    None,

    /// <summary>The components of the project.</summary>
    Components,

    /// <summary>The versions of the project.</summary>
    Versions,
}
