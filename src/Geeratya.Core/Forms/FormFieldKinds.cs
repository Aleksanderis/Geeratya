namespace Geeratya.Core.Forms;

/// <summary>Facts about <see cref="FormFieldKind"/> that the reducer, the validation and the renderer share.</summary>
public static class FormFieldKinds
{
    /// <summary>True when the user can type or choose a value; the other kinds are listed read-only.</summary>
    public static bool IsEditable(FormFieldKind kind)
    {
        return kind != FormFieldKind.Unsupported;
    }

    /// <summary>True for the kinds that pick one person or issue found by a search (the suggestion popup).</summary>
    public static bool IsPicked(FormFieldKind kind)
    {
        return kind is FormFieldKind.User or FormFieldKind.Parent;
    }

    /// <summary>True for the kinds that choose from the allowed values.</summary>
    public static bool IsChoice(FormFieldKind kind)
    {
        return kind is FormFieldKind.Select or FormFieldKind.MultiSelect;
    }

    /// <summary>True for the kinds whose value is the typed text.</summary>
    public static bool IsText(FormFieldKind kind)
    {
        return kind is FormFieldKind.Text or FormFieldKind.Paragraph or FormFieldKind.Number
            or FormFieldKind.Date or FormFieldKind.DateTime;
    }
}
