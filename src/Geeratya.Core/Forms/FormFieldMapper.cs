using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// Turns the three kinds of Jira field metadata (editmeta, createmeta, transition fields) into one list of form
/// fields. Pure and tolerant: nothing is listed in code, a schema type it does not know is
/// <see cref="FormFieldKind.Unsupported"/> (shown read-only, never dropped), and an unseen field renders by name.
/// Editable fields come first (required ones before optional ones), read-only fields last.
/// </summary>
public static class FormFieldMapper
{
    private const string StringType = "string";
    private const string NumberType = "number";
    private const string DateType = "date";
    private const string DateTimeType = "datetime";
    private const string ArrayType = "array";
    private const string UserType = "user";
    private const string OptionType = "option";
    private const string PriorityType = "priority";
    private const string ResolutionType = "resolution";
    private const string SecurityType = "securitylevel";
    private const string ComponentType = "component";
    private const string VersionType = "version";
    private const string IssueLinkType = "issuelink";
    private const string LabelsSystem = "labels";
    private const string ParentSystem = "parent";
    private const string DescriptionSystem = "description";
    private const string EnvironmentSystem = "environment";
    private const string TextAreaSuffix = ":textarea";

    /// <summary>The fields of an edit form for <paramref name="target"/>, with the values the opener knows.</summary>
    public static IReadOnlyList<FormField> FromEdit(IReadOnlyList<EditField> fields, EditFormTarget target)
    {
        var result = new List<FormField>();

        foreach (var field in fields)
        {
            var kind = field.IsEditable ? EditKind(field.Kind) : FormFieldKind.Unsupported;
            var known = target.Known.TryGetValue(field.FieldId, out var value);

            if (target.Only is { } only && kind != only)
            {
                continue;
            }

            if (target.OnlyKnown && !known && FormFieldKinds.IsEditable(kind))
            {
                continue;
            }

            var mapped = FormField.Create(field.FieldId, field.Name, kind, field.Required, field.AllowedValues);

            result.Add(known ? mapped.WithInitial(value!) : mapped);
        }

        return Ordered(result);
    }

    /// <summary>The fields of a create form. Project and issue type are set by the app and not listed.</summary>
    public static IReadOnlyList<FormField> FromCreate(IReadOnlyList<CreateField> fields)
    {
        var result = new List<FormField>();

        foreach (var field in fields.Where(candidate => candidate.Kind != CreateFieldKind.Fixed))
        {
            var options = field.AllowedValues.Select(value => new FieldOption(value.Id, value.Label)).ToArray();

            result.Add(FormField.Create(field.FieldId, field.Name, CreateKind(field.Kind), field.Required, options));
        }

        return Ordered(result);
    }

    /// <summary>The fields a transition screen asks for.</summary>
    public static IReadOnlyList<FormField> FromTransition(IReadOnlyList<FieldMeta> fields)
    {
        var result = new List<FormField>();

        foreach (var field in fields)
        {
            var kind = SchemaKind(field);

            result.Add(FormField.Create(field.FieldId, field.Name, kind, field.Required, field.AllowedValues));
        }

        return Ordered(result);
    }

    private static List<FormField> Ordered(List<FormField> fields)
    {
        var editable = fields.Where(field => field.IsEditable).OrderBy(field => field.Required ? 0 : 1);

        return [.. editable, .. fields.Where(field => !field.IsEditable)];
    }

    private static FormFieldKind EditKind(EditFieldKind kind)
    {
        return kind switch
        {
            EditFieldKind.Text => FormFieldKind.Text,
            EditFieldKind.RichText => FormFieldKind.Paragraph,
            EditFieldKind.Number => FormFieldKind.Number,
            EditFieldKind.Date => FormFieldKind.Date,
            EditFieldKind.DateTime => FormFieldKind.DateTime,
            EditFieldKind.Option => FormFieldKind.Select,
            EditFieldKind.MultiOption => FormFieldKind.MultiSelect,
            EditFieldKind.User => FormFieldKind.User,
            EditFieldKind.Labels => FormFieldKind.Labels,
            EditFieldKind.Parent => FormFieldKind.Parent,
            _ => FormFieldKind.Unsupported,
        };
    }

    // The create metadata does not tell a date from a date-time, so both are a date here; #162 refines it.
    private static FormFieldKind CreateKind(CreateFieldKind kind)
    {
        return kind switch
        {
            CreateFieldKind.Text => FormFieldKind.Text,
            CreateFieldKind.RichText => FormFieldKind.Paragraph,
            CreateFieldKind.SingleChoice => FormFieldKind.Select,
            CreateFieldKind.MultiChoice => FormFieldKind.MultiSelect,
            CreateFieldKind.Labels => FormFieldKind.Labels,
            CreateFieldKind.User => FormFieldKind.User,
            CreateFieldKind.Date => FormFieldKind.Date,
            CreateFieldKind.Number => FormFieldKind.Number,
            CreateFieldKind.Parent => FormFieldKind.Parent,
            _ => FormFieldKind.Unsupported,
        };
    }

    private static FormFieldKind SchemaKind(FieldMeta field)
    {
        return field.SchemaType switch
        {
            StringType => IsRichText(field) ? FormFieldKind.Paragraph : FormFieldKind.Text,
            NumberType => FormFieldKind.Number,
            DateType => FormFieldKind.Date,
            DateTimeType => FormFieldKind.DateTime,
            PriorityType or ResolutionType or SecurityType or OptionType => FormFieldKind.Select,
            VersionType or ComponentType => FormFieldKind.Select,
            UserType => FormFieldKind.User,
            IssueLinkType when field.SchemaSystem == ParentSystem => FormFieldKind.Parent,
            ArrayType => ArrayKind(field),
            _ => FormFieldKind.Unsupported,
        };
    }

    private static FormFieldKind ArrayKind(FieldMeta field)
    {
        if (field.SchemaSystem == LabelsSystem && field.SchemaItems == StringType)
        {
            return FormFieldKind.Labels;
        }

        return field.SchemaItems is OptionType or ComponentType or VersionType
            ? FormFieldKind.MultiSelect
            : FormFieldKind.Unsupported;
    }

    private static bool IsRichText(FieldMeta field)
    {
        return field.SchemaSystem is DescriptionSystem or EnvironmentSystem
            || field.SchemaCustom.EndsWith(TextAreaSuffix, StringComparison.Ordinal);
    }
}
