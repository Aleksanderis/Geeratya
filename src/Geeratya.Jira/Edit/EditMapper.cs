using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Transitions;

namespace Geeratya.Jira.Edit;

/// <summary>
/// Maps the edit metadata and decides each field's <see cref="EditFieldKind"/> from its schema. Pure and tolerant:
/// nothing here throws, an unknown schema type is <see cref="EditFieldKind.Unsupported"/> (read-only).
/// </summary>
internal static class EditMapper
{
    private const string StringType = "string";
    private const string NumberType = "number";
    private const string DateType = "date";
    private const string DateTimeType = "datetime";
    private const string ArrayType = "array";
    private const string UserType = "user";
    private const string IssueLinkType = "issuelink";
    private const string OptionType = "option";
    private const string PriorityType = "priority";
    private const string ResolutionType = "resolution";
    private const string SecurityType = "securitylevel";
    private const string ComponentType = "component";
    private const string VersionType = "version";
    private const string LabelsSystem = "labels";
    private const string ParentSystem = "parent";
    private const string DescriptionSystem = "description";
    private const string EnvironmentSystem = "environment";
    private const string TextAreaSuffix = ":textarea";
    private const string AssigneeSystem = "assignee";

    public static IReadOnlyList<EditField> Map(EditWire.Meta meta)
    {
        var result = new List<EditField>(meta.Fields?.Count ?? 0);

        foreach (var (key, field) in meta.Fields ?? [])
        {
            if (field is null)
            {
                continue;
            }

            var id = string.IsNullOrEmpty(field.Key) ? key : field.Key;
            var schema = field.Schema;
            var type = schema?.Type ?? string.Empty;
            var items = schema?.Items ?? string.Empty;

            result.Add(new EditField(
                id,
                field.Name ?? id,
                field.Required ?? false,
                type,
                items,
                [.. field.Operations ?? []],
                TransitionMapper.MapOptions(field.AllowedValues),
                field.HasDefaultValue ?? false,
                KindOf(schema)));
        }

        return result;
    }

    private static EditFieldKind KindOf(EditWire.Schema? schema)
    {
        var type = schema?.Type ?? string.Empty;
        var items = schema?.Items ?? string.Empty;

        return type switch
        {
            StringType => IsRichText(schema) ? EditFieldKind.RichText : EditFieldKind.Text,
            NumberType => EditFieldKind.Number,
            DateType => EditFieldKind.Date,
            DateTimeType => EditFieldKind.DateTime,
            PriorityType or ResolutionType or SecurityType or OptionType => EditFieldKind.Option,
            UserType => EditFieldKind.User,
            IssueLinkType when schema?.System == ParentSystem => EditFieldKind.Parent,
            ArrayType => ArrayKind(schema, items),
            _ => EditFieldKind.Unsupported,
        };
    }

    private static EditFieldKind ArrayKind(EditWire.Schema? schema, string items)
    {
        if (schema?.System == LabelsSystem && items == StringType)
        {
            return EditFieldKind.Labels;
        }

        return items is OptionType or ComponentType or VersionType
            ? EditFieldKind.MultiOption
            : EditFieldKind.Unsupported;
    }

    // In API v3 the description, the environment and multi-line text fields are ADF; other strings are plain.
    private static bool IsRichText(EditWire.Schema? schema) =>
        schema?.System is DescriptionSystem or EnvironmentSystem
        || schema?.Custom?.EndsWith(TextAreaSuffix, StringComparison.Ordinal) == true;

    /// <summary>True when the field is the assignee, which has its own endpoint and three distinct meanings.</summary>
    public static bool IsAssignee(string fieldId) => fieldId == AssigneeSystem;
}
