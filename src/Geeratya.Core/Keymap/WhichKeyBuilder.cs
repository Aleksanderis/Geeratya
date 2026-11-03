namespace Geeratya.Core.Keymap;

/// <summary>
/// The rows of the which-key popup, from the keymap and the action titles (never hand-written). Order: the plain
/// actions first, kept in clusters of actions that share an id prefix (<c>issue.*</c>, <c>rank.*</c>) with a blank row
/// between clusters and the lone actions last, then the <c>+noun (n)</c> groups; inside each part alphabetical by
/// label, lowercase before uppercase. Pure.
/// </summary>
public static class WhichKeyBuilder
{
    private const string GroupMarker = "+";

    private static readonly IComparer<WhichKeyRow> _rowOrder = Comparer<WhichKeyRow>.Create(CompareRows);

    public static IReadOnlyList<WhichKeyRow> Build(
        KeymapTable keymap,
        InputMode mode,
        string pending,
        CommandLevel level = CommandLevel.Advanced)
    {
        var plain = new List<(string Prefix, WhichKeyRow Row)>();
        var groups = new List<WhichKeyRow>();

        foreach (var next in keymap.NextKeys(mode, pending, level))
        {
            if (next.Action is null)
            {
                var noun = Noun(keymap, mode, $"{pending} {next.Key}", level);

                groups.Add(new WhichKeyRow(KeyNotation.Display(next.Key), $"{GroupMarker}{noun} ({next.Count})"));
            }
            else
            {
                var title = ActionCatalog.FindTitle(next.Action) ?? next.Action;

                plain.Add((PrefixOf(next.Action), new WhichKeyRow(KeyNotation.Display(next.Key), title)));
            }
        }

        var byPrefix = plain
            .GroupBy(entry => entry.Prefix, StringComparer.Ordinal)
            .Select(group => Sorted(group.Select(entry => entry.Row)))
            .ToList();
        var parts = byPrefix.Where(cluster => cluster.Count > 1).OrderBy(cluster => cluster[0], _rowOrder).ToList();

        parts.Add(Sorted(byPrefix.Where(cluster => cluster.Count == 1).Select(cluster => cluster[0])));
        parts.Add(Sorted(groups));

        var rows = new List<WhichKeyRow>();

        foreach (var part in parts.Where(part => part.Count > 0))
        {
            if (rows.Count > 0)
            {
                rows.Add(WhichKeyRow.Blank);
            }

            rows.AddRange(part);
        }

        return rows;
    }

    /// <summary>The rows without the blank separators, for a popup too short to show them.</summary>
    public static IReadOnlyList<WhichKeyRow> WithoutBlanks(IReadOnlyList<WhichKeyRow> rows)
    {
        return rows.Where(row => !row.IsBlank).ToList();
    }

    private static List<WhichKeyRow> Sorted(IEnumerable<WhichKeyRow> rows)
    {
        var list = rows.ToList();

        list.Sort(_rowOrder);

        return list;
    }

    /// <summary>Alphabetical by label ignoring case; equal labels put the lowercase key first.</summary>
    private static int CompareRows(WhichKeyRow? left, WhichKeyRow? right)
    {
        var byLabel = string.Compare(left!.Label, right!.Label, StringComparison.OrdinalIgnoreCase);

        return byLabel != 0 ? byLabel : string.CompareOrdinal(right.Key, left.Key);
    }

    private static string PrefixOf(string action)
    {
        var dot = action.IndexOf('.');

        return dot < 0 ? action : action[..dot];
    }

    private static string Noun(KeymapTable keymap, InputMode mode, string pending, CommandLevel level)
    {
        var next = keymap.NextKeys(mode, pending, level);

        while (next.Count > 0 && next[0].Action is null)
        {
            pending = $"{pending} {next[0].Key}";
            next = keymap.NextKeys(mode, pending, level);
        }

        var prefix = next.Count > 0 ? PrefixOf(next[0].Action!) : "keys";

        return prefix;
    }
}
