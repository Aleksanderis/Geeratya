namespace Geeratya.Core.Lists;

/// <summary>
/// The one text matcher of every pick list and filter (<c>docs/internal/ui-consistency.md</c>, component 5): case is
/// always ignored, and a query matches as a substring, or, for ranking, as letters in order (fzf style). Pure.
/// </summary>
public static class Matcher
{
    private const int ExactRank = 0;
    private const int PrefixRank = 1;
    private const int SubstringRank = 1000;
    private const int ScatteredRank = 100000;

    /// <summary>
    /// True when <paramref name="text"/> holds <paramref name="query"/> as a substring, case ignored; an empty query
    /// matches everything. The filter of the lists that keep their order (picker, tab list, sprints).
    /// </summary>
    public static bool Matches(string text, string query)
    {
        return query.Length == 0 || text.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True when any of <paramref name="texts"/> matches (see <see cref="Matches(string, string)"/>).</summary>
    public static bool MatchesAny(string query, params string?[] texts)
    {
        return query.Length == 0 || texts.Any(text => text is not null && Matches(text, query));
    }

    /// <summary>
    /// How well <paramref name="text"/> fits <paramref name="query"/>, lower is better, or -1 for no match: the exact
    /// text first, then a prefix, then a substring (the earlier the better), then letters in order (the tighter the
    /// better).
    /// </summary>
    public static int Rank(string text, string query)
    {
        if (string.Equals(text, query, StringComparison.OrdinalIgnoreCase))
        {
            return ExactRank;
        }

        if (text.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return PrefixRank;
        }

        var at = text.IndexOf(query, StringComparison.OrdinalIgnoreCase);

        if (at >= 0)
        {
            return SubstringRank + at;
        }

        return Spread(query, text) is { } spread ? ScatteredRank + spread : -1;
    }

    /// <summary>
    /// The length of the stretch of <paramref name="text"/> that holds the letters of <paramref name="query"/> in order
    /// (greedy from the left), or null when they do not appear in order.
    /// </summary>
    public static int? Spread(string query, string text)
    {
        var next = 0;
        var first = -1;
        var last = -1;

        for (var index = 0; index < text.Length && next < query.Length; index++)
        {
            if (char.ToUpperInvariant(text[index]) != char.ToUpperInvariant(query[next]))
            {
                continue;
            }

            first = first < 0 ? index : first;
            last = index;
            next++;
        }

        return next == query.Length ? last - first + 1 : null;
    }
}
