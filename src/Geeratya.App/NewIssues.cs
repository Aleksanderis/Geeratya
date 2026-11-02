using System.Globalization;
using Geeratya.Core.Forms;
using Geeratya.Core.Writes;
using Geeratya.Jira.Create.Models;

namespace Geeratya.App;

/// <summary>
/// Turns the filled fields of a create form into the typed <see cref="NewIssue"/> of the Jira layer. The summary and
/// the description have their own slots; every other field is written in the shape its kind needs. Validation ran
/// before, so a value that does not read is a bug and throws instead of writing something else.
/// </summary>
public static class NewIssues
{
    public static NewIssue For(CreateRequest request)
    {
        string? description = null;
        var fields = new Dictionary<string, CreateFieldValue>();

        foreach (var change in request.Changes.Where(change => !change.Value.IsEmpty))
        {
            // The parent has its own slot of the request, set from the form.
            if (change.FieldId is FormFieldIds.Summary or FormFieldIds.Parent)
            {
                continue;
            }

            if (change.FieldId == FormFieldIds.Description)
            {
                description = change.Value.Text;
            }
            else
            {
                fields[change.FieldId] = ValueOf(change);
            }
        }

        return new NewIssue(
            request.ProjectKey,
            request.IssueTypeId,
            request.Summary,
            description,
            request.ParentKey,
            fields,
            DescriptionIsMarkdown: true);
    }

    private static CreateFieldValue ValueOf(FieldChange change)
    {
        var text = change.Value.Text.Trim();
        var chosen = change.Value.Chosen;

        return change.Kind switch
        {
            FormFieldKind.Text => CreateFieldValue.Text(text),
            FormFieldKind.Paragraph => CreateFieldValue.Markdown(change.Value.Text),
            FormFieldKind.Number => CreateFieldValue.Number(Number(text)),
            FormFieldKind.Date => CreateFieldValue.Text(Date(text)),
            FormFieldKind.DateTime => CreateFieldValue.DateTime(DateTime(text)),
            FormFieldKind.Select => CreateFieldValue.Choice(chosen[0]),
            FormFieldKind.MultiSelect => CreateFieldValue.Choices(chosen),
            FormFieldKind.Labels => CreateFieldValue.Labels(chosen),
            FormFieldKind.User => CreateFieldValue.User(chosen[0]),
            _ => throw new NotSupportedException($"The field {change.FieldId} cannot be written."),
        };
    }

    private static decimal Number(string text)
    {
        return FormValueParser.TryNumber(text, out var number)
            ? (decimal)number
            : throw new FormatException("Not a number.");
    }

    private static string Date(string text)
    {
        return FormValueParser.TryDate(text, out var date)
            ? date.ToString(FormValueParser.DateFormat, CultureInfo.InvariantCulture)
            : throw new FormatException("Not a date.");
    }

    private static DateTimeOffset DateTime(string text)
    {
        return FormValueParser.TryDateTime(text, out var moment)
            ? moment
            : throw new FormatException("Not a moment.");
    }
}
