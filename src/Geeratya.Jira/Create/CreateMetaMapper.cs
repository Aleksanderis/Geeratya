using System.Text.Json;
using Geeratya.Jira.Create.Models;

namespace Geeratya.Jira.Create;

/// <summary>Maps create metadata to typed models. Pure and tolerant: nothing here throws on odd input.</summary>
internal static class CreateMetaMapper
{
    private const string TypeString = "string";
    private const string TypeOption = "option";
    private const string TypeArray = "array";
    private const string TypeUser = "user";
    private const string TypeDate = "date";
    private const string TypeDateTime = "datetime";
    private const string TypeNumber = "number";
    private const string TypePriority = "priority";
    private const string TypeComponent = "component";
    private const string TypeVersion = "version";
    private const string SystemDescription = "description";
    private const string SystemEnvironment = "environment";
    private const string CustomTextArea = ":textarea";

    public static IReadOnlyList<CreateIssueType> MapIssueTypes(IEnumerable<CreateWire.IssueType?> issueTypes)
    {
        var result = new List<CreateIssueType>();

        foreach (var issueType in issueTypes)
        {
            // An entry without an id cannot be created, so it is dropped.
            if (issueType?.Id is not { Length: > 0 } id)
            {
                continue;
            }

            result.Add(new CreateIssueType(
                id,
                issueType.Name ?? id,
                issueType.Description ?? string.Empty,
                issueType.HierarchyLevel ?? 0,
                issueType.Subtask ?? false));
        }

        return result;
    }

    public static IReadOnlyList<CreateField> MapFields(IEnumerable<CreateWire.Field?> fields)
    {
        var result = new List<CreateField>();

        foreach (var field in fields)
        {
            if (field?.FieldId is not { Length: > 0 } fieldId)
            {
                continue;
            }

            var kind = KindOf(fieldId, field.Schema);

            result.Add(new CreateField(
                fieldId,
                field.Name ?? fieldId,
                field.Required ?? false,
                field.HasDefaultValue ?? false,
                kind,
                MapAllowedValues(field.AllowedValues),
                field.AutoCompleteUrl,
                LookupOf(kind, field.Schema)));
        }

        return result;
    }

    private static CreateFieldKind KindOf(string fieldId, CreateWire.Schema? schema)
    {
        if (fieldId is CreateFieldIds.Project or CreateFieldIds.IssueType)
        {
            return CreateFieldKind.Fixed;
        }

        if (fieldId == CreateFieldIds.Parent)
        {
            return CreateFieldKind.Parent;
        }

        if (schema?.Type is not { } type)
        {
            return CreateFieldKind.Unsupported;
        }

        return type switch
        {
            TypeString => StringKind(schema),
            TypeOption or TypePriority => CreateFieldKind.SingleChoice,
            TypeUser => CreateFieldKind.User,
            TypeDate => CreateFieldKind.Date,
            TypeDateTime => CreateFieldKind.DateTime,
            TypeNumber => CreateFieldKind.Number,
            TypeArray => ArrayKind(schema),
            _ => CreateFieldKind.Unsupported,
        };
    }

    private static CreateLookup LookupOf(CreateFieldKind kind, CreateWire.Schema? schema)
    {
        if (kind != CreateFieldKind.MultiChoice)
        {
            return CreateLookup.None;
        }

        return schema?.Items switch
        {
            TypeComponent => CreateLookup.Components,
            TypeVersion => CreateLookup.Versions,
            _ => CreateLookup.None,
        };
    }

    private static CreateFieldKind StringKind(CreateWire.Schema schema)
    {
        var isRich = schema.System is SystemDescription or SystemEnvironment
            || schema.Custom?.EndsWith(CustomTextArea, StringComparison.Ordinal) == true;

        return isRich ? CreateFieldKind.RichText : CreateFieldKind.Text;
    }

    private static CreateFieldKind ArrayKind(CreateWire.Schema schema) =>
        schema.Items switch
        {
            TypeString => CreateFieldKind.Labels,
            TypeOption or TypeComponent or TypeVersion => CreateFieldKind.MultiChoice,
            _ => CreateFieldKind.Unsupported,
        };

    private static List<CreateAllowedValue> MapAllowedValues(List<JsonElement>? values)
    {
        var result = new List<CreateAllowedValue>();

        foreach (var value in values ?? [])
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = ReadText(value, "id");

            if (id is null)
            {
                continue;
            }

            var label = ReadText(value, "value") ?? ReadText(value, "name") ?? id;
            result.Add(new CreateAllowedValue(id, label));
        }

        return result;
    }

    private static string? ReadText(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
    }
}
