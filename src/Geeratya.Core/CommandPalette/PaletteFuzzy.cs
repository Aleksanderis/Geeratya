using Geeratya.Core.Lists;

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

        if (Matcher.Spread(filter, title) is { } titleSpread)
        {
            return TitleSubsequence - titleSpread;
        }

        return Matcher.Spread(filter, id) is { } idSpread ? IdSubsequence - idSpread : null;
    }
}
