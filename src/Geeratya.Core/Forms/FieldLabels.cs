namespace Geeratya.Core.Forms;

/// <summary>
/// The label of a field as every presentation draws it (<c>docs/internal/ui-consistency.md</c>, component 3): the
/// name, then the one required mark, a space and <c>*</c>, directly after it. The create form's title box, its Details
/// blocks and the boxes of the field forms all take the label from here, so a required field is marked in one place
/// and one way. The state of the form (navigate or editing) is never part of a label: it is in the status line.
/// </summary>
public static class FieldLabels
{
    /// <summary>The mark of a required field, after its label.</summary>
    public const string RequiredMark = " *";

    /// <summary>The label of <paramref name="field"/>, with the required mark when it is required.</summary>
    public static string Of(FormField field)
    {
        return With(field.Label, field.Required);
    }

    /// <summary><paramref name="label"/> with the required mark when <paramref name="required"/>.</summary>
    public static string With(string label, bool required)
    {
        return required ? $"{label}{RequiredMark}" : label;
    }
}
