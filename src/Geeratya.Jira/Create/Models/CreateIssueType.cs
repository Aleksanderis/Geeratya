namespace Geeratya.Jira.Create.Models;

/// <summary>
/// One issue type a project offers on the create screen. Match on <see cref="HierarchyLevel"/> and
/// <see cref="IsSubtask"/>, never on the name: names differ per kind and site.
/// </summary>
public sealed record CreateIssueType(
    string Id,
    string Name,
    string Description,
    int HierarchyLevel,
    bool IsSubtask);
