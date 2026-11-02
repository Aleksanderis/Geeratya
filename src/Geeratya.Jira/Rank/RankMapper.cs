using System.Text.Json;
using Geeratya.Jira.Rank.Models;

namespace Geeratya.Jira.Rank;

/// <summary>Maps the 204 (all moved) and 207 (per issue) replies to <see cref="RankOutcome"/>.</summary>
internal static class RankMapper
{
    private const int StatusOk = 200;
    private const int StatusLastSuccess = 299;

    /// <summary>
    /// A missing body means every issue was moved. In a 207 an issue Jira did not list is reported as not moved,
    /// never as moved (the caller reloads the view either way).
    /// </summary>
    public static RankOutcome Map(IReadOnlyList<string> requested, RankWire.Response? wire)
    {
        if (wire is null)
        {
            return new RankOutcome(requested.Select(key => new RankItemResult(key, true, [])).ToList());
        }

        var byKey = new Dictionary<string, RankItemResult>(StringComparer.Ordinal);

        foreach (var entry in wire.Entries ?? [])
        {
            if (entry?.IssueKey is { Length: > 0 } key)
            {
                var errors = ReadErrors(entry.Errors);
                var ok = entry.Status is >= StatusOk and <= StatusLastSuccess && errors.Count == 0;
                byKey[key] = new RankItemResult(key, ok, errors);
            }
        }

        return new RankOutcome(requested
            .Select(key => byKey.TryGetValue(key, out var item)
                ? item
                : new RankItemResult(key, false, ["Jira did not report this issue."]))
            .ToList());
    }

    private static List<string> ReadErrors(JsonElement? errors)
    {
        var list = new List<string>();

        if (errors is not { } element)
        {
            return list;
        }

        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                AddText(list, element);
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    AddText(list, item);
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    AddText(list, property.Value);
                }

                break;
        }

        return list;
    }

    private static void AddText(List<string> list, JsonElement item)
    {
        if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text)
        {
            list.Add(text);
        }
    }
}
