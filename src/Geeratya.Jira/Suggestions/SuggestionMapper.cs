using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Jira.Suggestions;

/// <summary>Maps suggestion replies to <see cref="Suggestion"/>. Pure and tolerant: nothing here throws.</summary>
internal static class SuggestionMapper
{
    public static IReadOnlyList<Suggestion> MapUsers(List<SuggestionWire.User?>? users)
    {
        var result = new List<Suggestion>(users?.Count ?? 0);

        foreach (var user in users ?? [])
        {
            if (user?.AccountId is { Length: > 0 } accountId)
            {
                var name = string.IsNullOrWhiteSpace(user.DisplayName) ? accountId : user.DisplayName;
                result.Add(new Suggestion(accountId, name, null, user.Active == false));
            }
        }

        return result;
    }

    public static IReadOnlyList<Suggestion> MapPicker(SuggestionWire.Picker? picker)
    {
        var result = new List<Suggestion>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var issue in (picker?.Sections ?? []).SelectMany(section => section?.Issues ?? []))
        {
            if (issue?.Key is { Length: > 0 } key && seen.Add(key))
            {
                var summary = issue.SummaryText ?? issue.Summary ?? string.Empty;
                result.Add(new Suggestion(key, summary, key, false));
            }
        }

        return result;
    }

    /// <summary>The rows with the issue type of each issue the search answered; rows it did not name stay as they are.</summary>
    public static IReadOnlyList<Suggestion> WithTypes(IReadOnlyList<Suggestion> rows, SuggestionWire.TypeSearch? search)
    {
        var types = new Dictionary<string, (string Name, int? Level)>(StringComparer.OrdinalIgnoreCase);

        foreach (var issue in search?.Issues ?? [])
        {
            if (issue?.Key is { Length: > 0 } key && issue.Fields?.Issuetype?.Name is { Length: > 0 } name)
            {
                types[key] = (name, issue.Fields.Issuetype.HierarchyLevel);
            }
        }

        return [.. rows.Select(row => types.TryGetValue(row.Id, out var type) ? row with { Kind = type.Name, HierarchyLevel = type.Level } : row)];
    }

    public static IReadOnlyList<Suggestion> MapNamed(List<SuggestionWire.Named?>? items)
    {
        var result = new List<Suggestion>(items?.Count ?? 0);

        foreach (var item in items ?? [])
        {
            if (item?.Id is { Length: > 0 } id && !string.IsNullOrWhiteSpace(item.Name))
            {
                result.Add(new Suggestion(id, item.Name, null, item.Archived == true));
            }
        }

        return result;
    }

    public static IReadOnlyList<string> MapLabels(SuggestionWire.LabelPage page) =>
        (page.Values ?? []).OfType<string>().Where(label => label.Length > 0).ToList();
}
