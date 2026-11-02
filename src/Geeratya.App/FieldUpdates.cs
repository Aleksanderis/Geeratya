using Geeratya.Core.Forms;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit.Models;

namespace Geeratya.App;

/// <summary>
/// Turns the changed fields of a form into the typed edits of the Jira layer, in the shape each kind needs. An empty
/// value clears the field. Validation ran before, so a value that does not read is a bug and throws rather than
/// writing a clearing null.
/// </summary>
public static class FieldUpdates
{
    public static IReadOnlyList<FieldUpdate> For(IReadOnlyList<FieldChange> changes)
    {
        return [.. changes.Select(Of)];
    }

    private static FieldUpdate Of(FieldChange change)
    {
        var text = change.Value.Text.Trim();
        var chosen = change.Value.Chosen;
        var first = chosen.Count > 0 ? chosen[0] : null;

        return change.Kind switch
        {
            FormFieldKind.Text => FieldUpdate.Text(change.FieldId, text.Length == 0 ? null : change.Value.Text),
            FormFieldKind.Paragraph => FieldUpdate.RichText(change.FieldId, RichText(change.Value.Text)),
            FormFieldKind.Number => FieldUpdate.Number(change.FieldId, Number(text)),
            FormFieldKind.Date => FieldUpdate.Date(change.FieldId, Date(text)),
            FormFieldKind.DateTime => FieldUpdate.DateTime(change.FieldId, DateTime(text)),
            FormFieldKind.Select => FieldUpdate.Option(change.FieldId, first),
            FormFieldKind.MultiSelect => FieldUpdate.Options(change.FieldId, chosen),
            FormFieldKind.Labels => FieldUpdate.Labels(change.FieldId, chosen),
            FormFieldKind.User => FieldUpdate.User(change.FieldId, first),
            FormFieldKind.Parent => FieldUpdate.Parent(change.FieldId, first),
            _ => throw new NotSupportedException($"The field {change.FieldId} cannot be written."),
        };
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
