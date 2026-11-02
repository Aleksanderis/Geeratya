using System.Text.Json;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Jira.Transitions;

/// <summary>Maps the transitions reply. Pure and tolerant: nothing here throws, unknown fields are ignored.</summary>
internal static class TransitionMapper
{
    private const string IdProperty = "id";
    private const string KeyProperty = "key";

    // Options use "value", statuses and resolutions "name"; the first one present is the label.
    private static readonly string[] _labelProperties = ["name", "value", "title", KeyProperty];

    public static IReadOnlyList<Transition> Map(TransitionWire.Page page)
    {
        var result = new List<Transition>(page.Transitions?.Count ?? 0);

        foreach (var item in page.Transitions ?? [])
        {
            // A transition the account cannot do right now (or one without an id to post) is not offered.
            if (item is null || item.IsAvailable == false || string.IsNullOrEmpty(item.Id))
            {
                continue;
            }

            result.Add(MapTransition(item));
        }

        return result;
    }

    private static Transition MapTransition(TransitionWire.Item item) =>
        new(
            item.Id ?? string.Empty,
            item.Name ?? string.Empty,
            new IssueStatus(
                item.To?.Id ?? string.Empty,
                item.To?.Name ?? item.Name ?? string.Empty,
                item.To?.StatusCategory?.Key ?? string.Empty),
            item.HasScreen ?? false,
            item.IsGlobal ?? false,
            MapFields(item.Fields));

    private static List<FieldMeta> MapFields(Dictionary<string, TransitionWire.Field?>? fields)
    {
        var result = new List<FieldMeta>(fields?.Count ?? 0);

        foreach (var (key, field) in fields ?? [])
        {
            var id = string.IsNullOrEmpty(field?.FieldId) ? key : field.FieldId;
            result.Add(new FieldMeta(
                id,
                field?.Name ?? id,
                field?.Required ?? false,
                field?.Schema?.Type ?? string.Empty,
                field?.HasDefaultValue ?? false,
                MapOptions(field?.AllowedValues),
                field?.Schema?.Items ?? string.Empty,
                field?.Schema?.System ?? string.Empty,
                field?.Schema?.Custom ?? string.Empty));
        }

        return result;
    }

    internal static List<FieldOption> MapOptions(List<JsonElement>? values)
    {
        var result = new List<FieldOption>(values?.Count ?? 0);

        foreach (var value in values ?? [])
        {
            if (value.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var id = ReadString(value, IdProperty) ?? ReadString(value, KeyProperty) ?? string.Empty;
            var label = _labelProperties
                .Select(name => ReadString(value, name))
                .FirstOrDefault(text => text is not null);

            // An entry with neither id nor label (a user entry, say) cannot be posted or shown.
            if (id.Length == 0 && string.IsNullOrEmpty(label))
            {
                continue;
            }

            result.Add(new FieldOption(id, label ?? id));
        }

        return result;
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
}
