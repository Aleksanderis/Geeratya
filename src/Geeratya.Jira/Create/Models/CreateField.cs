namespace Geeratya.Jira.Create.Models;

/// <summary>
/// One field of the create screen of a project and issue type. <see cref="FieldId"/> is the id to write
/// (<c>summary</c>, <c>customfield_10001</c>); custom ids differ per site and are never hard-coded.
/// </summary>
public sealed record CreateField(
    string FieldId,
    string Name,
    bool Required,
    bool HasDefaultValue,
    CreateFieldKind Kind,
    IReadOnlyList<CreateAllowedValue> AllowedValues,
    string? AutoCompleteUrl);
