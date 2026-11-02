namespace Geeratya.Core.Sources;

/// <summary>
/// Matches a name typed in the palette: an exact match (case-insensitive) wins, otherwise a prefix that fits
/// exactly one name. Pure.
/// </summary>
public static class NameMatcher
{
    public static NameMatch Find(IReadOnlyList<string> names, string text)
    {
        var exact = Matching(names, text, prefix: false);

        return ToMatch(exact.Count > 0 ? exact : Matching(names, text, prefix: true));
    }

    private static List<int> Matching(IReadOnlyList<string> names, string text, bool prefix)
    {
        var found = new List<int>();

        for (var index = 0; index < names.Count; index++)
        {
            var name = names[index];
            var matches = prefix
                ? name.StartsWith(text, StringComparison.OrdinalIgnoreCase)
                : name.Equals(text, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                found.Add(index);
            }
        }

        return found;
    }

    private static NameMatch ToMatch(List<int> found)
    {
        return found.Count switch
        {
            0 => new NameMatch(NameMatchKind.None, -1, 0),
            1 => new NameMatch(NameMatchKind.Unique, found[0], 1),
            _ => new NameMatch(NameMatchKind.Ambiguous, -1, found.Count),
        };
    }
}
