namespace Geeratya.Core.CommandPalette;

/// <summary>Fuzzy matching of the palette filter against an action's title and id. Pure.</summary>
public static class PaletteFuzzy
{
    private const int TitleSubstring = 4000;
    private const int IdSubstring = 3000;
    private const int TitleSubsequence = 2000;
    private const int IdSubsequence = 1000;

    /// <summary>
    /// How well <paramref name="filter"/> matches (higher is better), or null when it does not: a substring of the
    /// title beats one of the id, which beats letters in order in the title, then in the id. Case is ignored; an
    /// earlier or tighter match scores higher; an empty filter matches everything equally.
    /// </summary>
    public static int? Score(string filter, string title, string id)
    {
        if (filter.Length == 0)
        {
            return 0;
        }

        var inTitle = title.IndexOf(filter, StringComparison.OrdinalIgnoreCase);

        if (inTitle >= 0)
        {
            return TitleSubstring - inTitle;
        }

        var inId = id.IndexOf(filter, StringComparison.OrdinalIgnoreCase);

        if (inId >= 0)
        {
            return IdSubstring - inId;
        }

        if (Spread(filter, title) is { } titleSpread)
        {
            return TitleSubsequence - titleSpread;
        }

        return Spread(filter, id) is { } idSpread ? IdSubsequence - idSpread : null;
    }

    // The length of the stretch that holds the filter's letters in order (greedy from the left); null if they do not.
    private static int? Spread(string filter, string text)
    {
        var next = 0;
        var first = -1;
        var last = -1;

        for (var index = 0; index < text.Length && next < filter.Length; index++)
        {
            if (char.ToUpperInvariant(text[index]) != char.ToUpperInvariant(filter[next]))
            {
                continue;
            }

            first = first < 0 ? index : first;
            last = index;
            next++;
        }

        return next == filter.Length ? last - first + 1 : null;
    }
}
