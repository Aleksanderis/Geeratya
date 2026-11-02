using System.Globalization;
using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search;

namespace Geeratya.Jira.Issues;

/// <summary>
/// Maps the raw JSON value of one field to <see cref="IssueFieldValue"/> by the kind the edit metadata decided. Pure
/// and tolerant: it never throws. Only a JSON null (or an empty list) is an empty value; a value of an unexpected
/// shape gives null, so the field is not offered and nothing is overwritten blind.
/// </summary>
internal static class IssueFieldValueMapper
{
    private const string IdProperty = "id";

    public static IssueFieldValue? Map(EditFieldKind kind, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null)
        {
            return IssueFieldValue.Empty;
        }

        return kind switch
        {
            EditFieldKind.Text or EditFieldKind.Date when value.ValueKind == JsonValueKind.String =>
                IssueFieldValue.Empty with { Text = value.GetString() ?? string.Empty },
            EditFieldKind.Number when value.ValueKind == JsonValueKind.Number =>
                IssueFieldValue.Empty with { Text = value.GetDouble().ToString(CultureInfo.InvariantCulture) },
            EditFieldKind.DateTime when value.ValueKind == JsonValueKind.String => MapMoment(value),
            EditFieldKind.RichText when value.ValueKind == JsonValueKind.Object => MapDocument(value),
            EditFieldKind.Option when value.ValueKind == JsonValueKind.Object => MapOption(value),
            EditFieldKind.MultiOption when value.ValueKind == JsonValueKind.Array => MapOptions(value),
            _ => null,
        };
    }

    private static IssueFieldValue? MapMoment(JsonElement value)
    {
        var moment = IssueSummaryMapper.ParseTimestamp(value.GetString());

        return moment is null ? null : IssueFieldValue.Empty with { Moment = moment };
    }

    private static IssueFieldValue? MapDocument(JsonElement value)
    {
        var document = AdfParser.Parse(value);

        return document.Content.Count == 0 ? null : IssueFieldValue.Empty with { Document = document };
    }

    private static IssueFieldValue? MapOption(JsonElement value)
    {
        return IdOf(value) is { } id ? IssueFieldValue.Empty with { Ids = [id] } : null;
    }

    private static IssueFieldValue? MapOptions(JsonElement value)
    {
        var ids = new List<string>();

        foreach (var item in value.EnumerateArray())
        {
            if (IdOf(item) is not { } id)
            {
                return null;
            }

            ids.Add(id);
        }

        return IssueFieldValue.Empty with { Ids = ids };
    }

    private static string? IdOf(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(IdProperty, out var id)
            && id.ValueKind == JsonValueKind.String
            && id.GetString() is { Length: > 0 } text
                ? text
                : null;
    }
}
