using Geeratya.Jira.Create.Models;

namespace Geeratya.Jira.Create;

/// <summary>
/// Reads what the create metadata of one issue type asks for. Pure: nothing here is keyed on an issue type name,
/// only on the fields Jira lists.
/// </summary>
public static class CreateFormRules
{
    /// <summary>Whether the type takes a parent: no <c>parent</c> field means not allowed.</summary>
    public static CreateParentRule ParentRule(IEnumerable<CreateField> fields)
    {
        foreach (var field in fields)
        {
            if (field.FieldId == CreateFieldIds.Parent)
            {
                return field.Required ? CreateParentRule.Required : CreateParentRule.Optional;
            }
        }

        return CreateParentRule.NotAllowed;
    }

    /// <summary>
    /// The required fields the issue still lacks. Project, issue type and fields with a default value (the team
    /// managed reporter) are filled by Jira or the client and never listed.
    /// </summary>
    public static IReadOnlyList<CreateField> MissingRequired(IEnumerable<CreateField> fields, NewIssue issue)
    {
        var missing = new List<CreateField>();

        foreach (var field in fields)
        {
            var isFilledByJira = field.HasDefaultValue || field.Kind == CreateFieldKind.Fixed;

            if (field.Required && !isFilledByJira && !IsProvided(field, issue))
            {
                missing.Add(field);
            }
        }

        return missing;
    }

    private static bool IsProvided(CreateField field, NewIssue issue) =>
        field.FieldId switch
        {
            CreateFieldIds.Summary => !string.IsNullOrWhiteSpace(issue.Summary),
            CreateFieldIds.Description => !string.IsNullOrWhiteSpace(issue.Description) || HasValue(field, issue),
            CreateFieldIds.Parent => !string.IsNullOrWhiteSpace(issue.ParentKey),
            _ => HasValue(field, issue),
        };

    private static bool HasValue(CreateField field, NewIssue issue) =>
        issue.Fields is not null
        && issue.Fields.TryGetValue(field.FieldId, out var value)
        && value.Values.Any(text => !string.IsNullOrWhiteSpace(text));
}
