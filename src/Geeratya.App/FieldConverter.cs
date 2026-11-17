using Geeratya.Core.Forms;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit.Models;

namespace Geeratya.App;

/// <summary>
/// Turns the changed fields of a form into the typed values of the Jira layer, in the shape each kind needs. One
/// converter serves the edit and the create form, so the same value sends the same JSON. An empty value clears the
/// field (a create leaves empty fields out before it gets here). Validation ran before, so a value that does not
/// read is a bug and throws rather than writing a clearing null. Sets of labels and options are sent as add and
/// remove changes in an edit, and whole in a create (a create has no <c>update</c> section).
/// </summary>
public static class FieldConverter
{
    /// <summary>The edits for the changed fields of an edit form.</summary>
    public static IReadOnlyList<FieldUpdate> ForEdit(IReadOnlyList<FieldChange> changes)
    {
        return [.. changes.Select(change => Convert(change, incremental: true))];
    }

    /// <summary>The value of one field of a create form, written whole.</summary>
    public static FieldUpdate ForCreate(FieldChange change)
    {
        return Convert(change, incremental: false);
    }

    private static FieldUpdate Convert(FieldChange change, bool incremental)
    {
        var text = change.Value.Text.Trim();
        var chosen = change.Value.Chosen;
        var first = chosen.Count > 0 ? chosen[0] : null;

        return change.Kind switch
        {
            FormFieldKind.Text => FieldUpdate.Text(change.FieldId, text.Length == 0 ? null : text),
            FormFieldKind.Paragraph => FieldUpdate.RichText(change.FieldId, RichText(change.Value.Text)),
            FormFieldKind.Number => FieldUpdate.Number(change.FieldId, Number(text)),
            FormFieldKind.Date => FieldUpdate.Date(change.FieldId, Date(text)),
            FormFieldKind.DateTime => FieldUpdate.DateTime(change.FieldId, DateTime(text)),
            FormFieldKind.Select => FieldUpdate.Option(change.FieldId, first),
            FormFieldKind.MultiSelect => ChangedOptions(change, incremental),
            FormFieldKind.Labels => ChangedLabels(change, incremental),
            FormFieldKind.User => FieldUpdate.User(change.FieldId, first),
            FormFieldKind.Parent => FieldUpdate.Parent(change.FieldId, first),
            _ => throw new NotSupportedException($"The field {change.FieldId} cannot be written."),
        };
    }

    // Adds and removes leave what someone else changed since the form was opened alone; without a known initial
    // value the whole set is the change.
    private static FieldUpdate ChangedLabels(FieldChange change, bool incremental)
    {
        if (!incremental || change.Initial is not { } initial)
        {
            return FieldUpdate.Labels(change.FieldId, change.Value.Chosen);
        }

        var added = change.Value.Chosen.Except(initial.Chosen).ToArray();
        var removed = initial.Chosen.Except(change.Value.Chosen).ToArray();

        return FieldUpdate.ChangeLabels(change.FieldId, added, removed);
    }

    private static FieldUpdate ChangedOptions(FieldChange change, bool incremental)
    {
        if (!incremental || change.Initial is not { } initial)
        {
            return FieldUpdate.Options(change.FieldId, change.Value.Chosen);
        }

        var added = change.Value.Chosen.Except(initial.Chosen).ToArray();
        var removed = initial.Chosen.Except(change.Value.Chosen).ToArray();

        return FieldUpdate.ChangeOptions(change.FieldId, added, removed);
    }

    private static AdfDocument? RichText(string text)
    {
        return text.Trim().Length == 0 ? null : AdfMarkdownParser.Parse(text);
    }

    private static double? Number(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        return FormValueParser.TryNumber(text, out var number) ? number : throw new FormatException("Not a number.");
    }

    private static DateOnly? Date(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        return FormValueParser.TryDate(text, out var date) ? date : throw new FormatException("Not a date.");
    }

    private static DateTimeOffset? DateTime(string text)
    {
        if (text.Length == 0)
        {
            return null;
        }

        return FormValueParser.TryDateTime(text, out var moment) ? moment : throw new FormatException("Not a moment.");
    }
}
