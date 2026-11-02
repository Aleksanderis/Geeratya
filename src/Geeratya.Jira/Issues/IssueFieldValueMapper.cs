using System.Globalization;
using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search;

namespace Geeratya.Jira.Issues;

/// <summary>
/// Maps the raw JSON value of one field to <see cref="IssueFieldValue"/> by the kind the edit metadata decided. Pure
/// and tolerant: a value of an unexpected shape reads as empty, never as an exception.
/// </summary>
internal static class IssueFieldValueMapper
{
    private const string IdProperty = "id";

    public static IssueFieldValue Map(EditFieldKind kind, JsonElement value)
    {
        return kind switch
        {
            EditFieldKind.Text or EditFieldKind.Date when value.ValueKind == JsonValueKind.String =>
                IssueFieldValue.Empty with { Text = value.GetString() ?? string.Empty },
            EditFieldKind.Number when value.ValueKind == JsonValueKind.Number =>
                IssueFieldValue.Empty with { Text = value.GetDouble().ToString(CultureInfo.InvariantCulture) },
            EditFieldKind.DateTime when value.ValueKind == JsonValueKind.String =>
                IssueFieldValue.Empty with { Moment = IssueSummaryMapper.ParseTimestamp(value.GetString()) },
            EditFieldKind.RichText when value.ValueKind == JsonValueKind.Object => MapDocument(value),
            EditFieldKind.Option when value.ValueKind == JsonValueKind.Object =>
                IssueFieldValue.Empty with { Ids = IdsOf(value) },
            EditFieldKind.MultiOption when value.ValueKind == JsonValueKind.Array =>
                IssueFieldValue.Empty with { Ids = [.. value.EnumerateArray().SelectMany(IdsOf)] },
            _ => IssueFieldValue.Empty,
        };
    }

    private static IssueFieldValue MapDocument(JsonElement value)
    {
        var document = AdfParser.Parse(value);

        return document.Content.Count == 0 ? IssueFieldValue.Empty : IssueFieldValue.Empty with { Document = document };
    }

    private static string[] IdsOf(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(IdProperty, out var id)
            && id.ValueKind == JsonValueKind.String
            && id.GetString() is { Length: > 0 } text
                ? [text]
                : [];
    }
}
