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

    /// <summary>True for the kinds that keep a typing buffer next to their value (labels and the picked kinds).</summary>
    public static bool TakesEntry(FormFieldKind kind)
    {
        return kind == FormFieldKind.Labels || IsPicked(kind);
    }

    /// <summary>True for the kinds whose value is chosen from a popup, not typed: the dropdown ends their block.</summary>
    public static bool HasPopup(FormFieldKind kind)
    {
        return IsChoice(kind) || TakesEntry(kind);
    }

    /// <summary>True for the kinds whose typed text has a format that is checked as it is typed.</summary>
    public static bool HasFormat(FormFieldKind kind)
    {
        return kind is FormFieldKind.Number or FormFieldKind.Date or FormFieldKind.DateTime;
    }

    /// <summary>The label with the format of a date or date-time field spelled out (once), so it shows before an error.</summary>
    public static string Labelled(string label, FormFieldKind kind)
    {
        var format = kind switch
        {
            FormFieldKind.Date => FormValueParser.DateFormat,
            FormFieldKind.DateTime => FormValueParser.DateTimeFormat,
            _ => null,
        };

        return format is null || label.Contains(format, StringComparison.Ordinal) ? label : $"{label} ({format})";
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
