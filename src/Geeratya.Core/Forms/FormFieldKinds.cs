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

    /// <summary>
    /// True for the kinds whose value is one chosen row: a single choice, a person, a parent. Taking the row finishes
    /// the field (Enter or a click leaves editing for the navigate state, or saves a form of one field), the same on
    /// every presentation (#1021). The kinds that collect several values (labels, a multiple choice) stay in editing
    /// so more can be taken.
    /// </summary>
    public static bool TakesOneValue(FormFieldKind kind)
    {
        return kind == FormFieldKind.Select || IsPicked(kind);
    }

    /// <summary>True for the kinds that keep a typing buffer next to their value (labels, picked kinds).</summary>
    public static bool TakesEntry(FormFieldKind kind)
    {
        return kind == FormFieldKind.Labels || IsPicked(kind);
    }

    /// <summary>True for the kinds whose value is chosen from a popup, not typed (the dropdown ends their block).</summary>
    public static bool HasPopup(FormFieldKind kind)
    {
        return IsChoice(kind) || TakesEntry(kind);
    }

    /// <summary>True for the single-line typed kinds: text, number, date and date-time (not a paragraph).</summary>
    public static bool IsTyped(FormFieldKind kind)
    {
        return kind is FormFieldKind.Text or FormFieldKind.Number or FormFieldKind.Date or FormFieldKind.DateTime;
    }

    /// <summary>
    /// True when <paramref name="field"/> is edited in a box under its row (the one look, #1048): every picker kind and
    /// every typed Details field. The Summary and Description are panes with their own in-pane editors, so they are
    /// not.
    /// </summary>
    public static bool HasEditBox(FormField field)
    {
        if (field.Id is FormFieldIds.Summary or FormFieldIds.Description)
        {
            return false;
        }

        return HasPopup(field.Kind) || IsTyped(field.Kind);
    }

    /// <summary>True for the kinds whose typed text has a format that is checked as it is typed.</summary>
    public static bool HasFormat(FormFieldKind kind)
    {
        return kind is FormFieldKind.Number or FormFieldKind.Date or FormFieldKind.DateTime;
    }

    /// <summary>
    /// The label without the format that <see cref="Labelled"/> added: the name for a status line prompt, where the
    /// format is already in the title of the field's box and would push the key hints out at 80 columns (#1046).
    /// </summary>
    public static string Named(FormField field)
    {
        var format = field.Kind switch
        {
            FormFieldKind.Date => FormValueParser.DateFormat,
            FormFieldKind.DateTime => FormValueParser.DateTimeFormat,
            _ => null,
        };
        var suffix = $" ({format})";

        return format is not null && field.Label.EndsWith(suffix, StringComparison.Ordinal)
            ? field.Label[..^suffix.Length]
            : field.Label;
    }

    /// <summary>The label with the format of a date or date-time field spelled out once, before any error.</summary>
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
