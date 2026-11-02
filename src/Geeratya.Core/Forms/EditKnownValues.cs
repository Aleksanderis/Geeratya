using System.Globalization;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// Completes the known values of an edit form from the issue itself. The opener passes what the row or detail holds;
/// every other editable field of the edit metadata (due date, components, versions, estimate, custom fields) has a
/// current value only in the issue, which is read with exactly those field ids. Pure.
/// </summary>
public static class EditKnownValues
{
    /// <summary>
    /// The editable fields whose current value the target does not hold and the form would offer: none when the form
    /// shows every field regardless (<c>OnlyKnown</c> off).
    /// </summary>
    public static IReadOnlyList<EditField> Missing(IReadOnlyList<EditField> fields, EditFormTarget target)
    {
        if (!target.OnlyKnown)
        {
            return [];
        }

        return
        [
            .. fields.Where(field => NeedsValue(field, target)),
        ];
    }

    /// <summary>
    /// <paramref name="target"/> with the read <paramref name="values"/> added as known: date text, option ids and
    /// component or version ids. A rich text value that would not survive the Markdown-like text is listed read-only.
    /// </summary>
    public static EditFormTarget Merge(EditFormTarget target, IReadOnlyDictionary<string, IssueFieldValue> values)
    {
        var known = new Dictionary<string, FormValue>(target.Known);
        var lossy = new List<string>(target.ReadOnlyIds ?? []);

        foreach (var (fieldId, value) in values)
        {
            if (value.Document is { } document && !AdfRoundTrip.RoundTrips(document))
            {
                lossy.Add(fieldId);

                continue;
            }

            known[fieldId] = ToFormValue(value);
        }

        return target with { Known = known, ReadOnlyIds = lossy };
    }

    private static bool NeedsValue(EditField field, EditFormTarget target)
    {
        if (!field.IsEditable || target.Known.ContainsKey(field.FieldId) || !IsReadable(field.Kind))
        {
            return false;
        }

        if (target.ReadOnlyIds?.Contains(field.FieldId) == true)
        {
            return false;
        }

        var matchesOnly = target.Only is not { } only || FormFieldMapper.EditKind(field.Kind) == only;

        return matchesOnly && (target.OnlyId is not { } id || field.FieldId == id);
    }

    // User and parent values need a display text the issue read does not give in this shape; they stay unoffered.
    private static bool IsReadable(EditFieldKind kind)
    {
        return kind is EditFieldKind.Text
            or EditFieldKind.RichText
            or EditFieldKind.Number
            or EditFieldKind.Date
            or EditFieldKind.DateTime
            or EditFieldKind.Option
            or EditFieldKind.MultiOption;
    }

    private static FormValue ToFormValue(IssueFieldValue value)
    {
        if (value.Ids.Count > 0)
        {
            return FormValue.OfChosen(value.Ids);
        }

        if (value.Document is { } document)
        {
            return FormValue.OfText(AdfMarkdownWriter.ToText(document));
        }

        if (value.Moment is { } moment)
        {
            var local = moment.ToLocalTime().ToString(FormValueParser.DateTimeFormat, CultureInfo.InvariantCulture);

            return FormValue.OfText(local);
        }

        return FormValue.OfText(value.Text);
    }
}
