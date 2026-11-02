namespace Geeratya.Core.Forms;

/// <summary>
/// The local check before a form is sent: a required field left empty and a number or date that does not read. Only
/// editable fields are checked; Jira decides about the rest and its answer is mapped to the field.
/// </summary>
public static class FormValidation
{
    public const string RequiredText = "Required";
    public const string NumberText = "Not a number";
    public const string DateText = "Use yyyy-MM-dd";
    public const string DateTimeText = "Use yyyy-MM-dd HH:mm";

    /// <summary>The fields with <see cref="FormField.Error"/> set where wrong and cleared where fine.</summary>
    public static IReadOnlyList<FormField> Check(IReadOnlyList<FormField> fields)
    {
        return [.. fields.Select(Check)];
    }

    private static FormField Check(FormField field)
    {
        return field.IsEditable ? field with { Error = ErrorOf(field) } : field;
    }

    private static string? ErrorOf(FormField field)
    {
        var empty = FormFieldKinds.IsText(field.Kind) ? field.Value.Text.Trim().Length == 0 : field.Value.IsEmpty;

        if (empty)
        {
            return field.Required ? RequiredText : null;
        }

        return field.Kind switch
        {
            FormFieldKind.Number => FormValueParser.TryNumber(field.Value.Text, out _) ? null : NumberText,
            FormFieldKind.Date => FormValueParser.TryDate(field.Value.Text, out _) ? null : DateText,
            FormFieldKind.DateTime => FormValueParser.TryDateTime(field.Value.Text, out _) ? null : DateTimeText,
            _ => null,
        };
    }
}
