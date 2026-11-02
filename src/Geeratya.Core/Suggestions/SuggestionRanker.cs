using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// Local fuzzy ranking of the candidates loaded so far (fzf style): the query's characters must appear in order, in
/// the label or the detail. Prefix matches rank first, then contiguous ones, then scattered ones (the tighter the
/// better); equal scores keep the given order. Pure. An empty query matches nothing.
/// </summary>
public static class SuggestionRanker
{
    /// <summary>The detail of the row that offers the typed text itself as a new value.</summary>
    public const string NewValueDetail = "new";

    private const int PrefixScore = 0;
    private const int SubstringScore = 1000;
    private const int ScatteredScore = 100000;

    /// <summary>
    /// The best matches (at most <see cref="SuggestionFilter.MaxResults"/>), without those in
    /// <paramref name="excluded"/>. With <paramref name="offerNew"/> a last row offers the query itself unless a
    /// candidate has exactly that label.
    /// </summary>
    public static IReadOnlyList<Suggestion> Rank(
        IEnumerable<Suggestion> pool,
        string query,
        IReadOnlyCollection<string> excluded,
        bool offerNew)
    {
        var text = query.Trim();

        if (text.Length == 0)
        {
            return [];
        }

        var ranked = pool
            .DistinctBy(candidate => candidate.Id, StringComparer.Ordinal)
            .Where(candidate => !excluded.Contains(candidate.Id))
            .Select((candidate, index) => new Scored(candidate, Score(candidate, text), index))
            .Where(scored => scored.Score >= 0)
            .OrderBy(scored => scored.Score)
            .ThenBy(scored => scored.Index)
            .Take(SuggestionFilter.MaxResults)
            .Select(scored => scored.Candidate)
            .ToList();

        var exact = ranked.Any(candidate => string.Equals(candidate.Label, text, StringComparison.OrdinalIgnoreCase));

        if (offerNew && !exact && !excluded.Contains(text))
        {
            ranked.Add(new Suggestion(text, text, NewValueDetail, false));
        }

        return ranked;
    }

    private static int Score(Suggestion candidate, string query)
    {
        var best = Score(candidate.Label, query);

        if (candidate.Detail is { Length: > 0 } detail)
        {
            var other = Score(detail, query);

            best = best < 0 || (other >= 0 && other < best) ? other : best;
        }

        return best;
    }

    private static int Score(string text, string query)
    {
        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return PrefixScore;
        }

        var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        if (at >= 0)
        {
            return SubstringScore + at;
        }

        return Scattered(text, query);
    }

    private static int Scattered(string text, string query)
    {
        var first = -1;
        var position = 0;

        foreach (var wanted in query)
        {
            var rest = text.AsSpan(position);
            var offset = rest.IndexOf(new ReadOnlySpan<char>(in wanted), StringComparison.OrdinalIgnoreCase);

            if (offset < 0)
            {
                return -1;
            }

            var found = position + offset;

            first = first < 0 ? found : first;
            position = found + 1;
        }

        return ScatteredScore + (position - first);
    }

    private sealed record Scored(Suggestion Candidate, int Score, int Index);
}
