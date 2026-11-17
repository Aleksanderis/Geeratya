using Geeratya.Core.Lists;
using Geeratya.Jira.Suggestions;
using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// Local fuzzy ranking of the candidates loaded so far (fzf style): the query's characters must appear in order, in
/// the label or the detail. An exact match ranks first, then prefix matches, then contiguous ones, then scattered ones (the tighter the
/// better); equal scores keep the given order. Pure. An empty query matches nothing.
/// </summary>
public static class SuggestionRanker
{
    /// <summary>The detail of the row that offers the typed text itself as a new value.</summary>
    public const string NewValueDetail = "new";

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
            .Select((candidate, index) => new Scored(candidate, ScoreOf(candidate, text), index))
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

    private static int ScoreOf(Suggestion candidate, string query)
    {
        var best = Matcher.Rank(candidate.Label, query);

        if (candidate.Detail is { Length: > 0 } detail)
        {
            var other = Matcher.Rank(detail, query);

            best = best < 0 || (other >= 0 && other < best) ? other : best;
        }

        return best;
    }

    private sealed record Scored(Suggestion Candidate, int Score, int Index);
}
