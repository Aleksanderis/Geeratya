using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Jira.Suggestions;

/// <summary>
/// Local filtering and ranking for sources Jira cannot search itself (labels, components, versions, allowed
/// values). Pure: same input, same output. An empty query matches nothing, so a picker never lists a whole site.
/// </summary>
public static class SuggestionFilter
{
    /// <summary>Most entries any source returns.</summary>
    public const int MaxResults = 20;

    /// <summary>
    /// Keeps entries whose label contains <paramref name="query"/> (ignoring case), prefix matches first, then in
    /// the given order, at most <see cref="MaxResults"/>, without duplicate ids.
    /// </summary>
    public static IReadOnlyList<Suggestion> Filter(IEnumerable<Suggestion> candidates, string? query)
    {
        var text = query?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            return [];
        }

        return candidates
            .DistinctBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Where(candidate => candidate.Label.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(candidate => candidate.Label.StartsWith(text, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Take(MaxResults)
            .ToList();
    }

    /// <summary>A label as a suggestion: the text is both id and label.</summary>
    public static Suggestion FromLabel(string label) => new(label, label, null, false);
}
