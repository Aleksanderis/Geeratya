namespace Geeratya.Core.Keymap;

/// <summary>
/// The rows of the which-key popup, from the keymap and the action titles (never hand-written). Order: the plain
/// actions first, kept in clusters of actions that share an id prefix (<c>issue.*</c>, <c>rank.*</c>) each under a
/// header row naming the cluster (a blank row before every header but the first) and the lone actions last under
/// "Other", then the <c>+noun (n)</c> groups under "More"; inside each part alphabetical by label, lowercase before
/// uppercase. The headers are left out when nothing but one part exists. Pure.
/// </summary>
public static class WhichKeyBuilder
{
    private const string GroupMarker = "+";
    private const string OtherHeader = "Other";
    private const string MoreHeader = "More";
    private const string NoActionsNoun = "keys";

    private static readonly char[] _wordSeparators = ['.', '-'];

    /// <summary>Group nouns whose id word is not how the UI spells it.</summary>
    private static readonly Dictionary<string, string> _nounText = new(StringComparer.Ordinal)
    {
        ["subtask"] = "sub-task",
    };

    private sealed record Part(string Name, List<WhichKeyRow> Rows);

    private static readonly IComparer<WhichKeyRow> _rowOrder = Comparer<WhichKeyRow>.Create(CompareRows);

    public static IReadOnlyList<WhichKeyRow> Build(
        KeymapTable keymap,
        InputMode mode,
        string pending,
        CommandLevel level = CommandLevel.Advanced,
        ActionContext? context = null)
    {
        var plain = new List<(string Prefix, WhichKeyRow Row)>();
        var groups = new List<WhichKeyRow>();

        foreach (var next in keymap.NextKeys(mode, pending, level, context))
        {
            if (next.Action is null)
            {
                var noun = Noun(keymap, mode, $"{pending} {next.Key}", level, context);

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
        var parts = byPrefix
            .Where(cluster => cluster.Count > 1)
            .OrderBy(cluster => cluster[0], _rowOrder)
            .Select(cluster => new Part(GroupTitle(PrefixOfRow(plain, cluster[0])), cluster))
            .ToList();

        var lone = Sorted(byPrefix.Where(cluster => cluster.Count == 1).Select(cluster => cluster[0]));

        parts.Add(new Part(OtherHeader, lone));
        parts.Add(new Part(MoreHeader, Sorted(groups)));
        parts = [.. parts.Where(part => part.Rows.Count > 0)];

        var rows = new List<WhichKeyRow>();
        var headed = parts.Count > 1;

        foreach (var part in parts)
        {
            if (rows.Count > 0)
            {
                rows.Add(WhichKeyRow.Blank);
            }

            if (headed)
            {
                rows.Add(WhichKeyRow.Header(part.Name));
            }

            rows.AddRange(part.Rows);
        }

        return rows;
    }

    /// <summary>The header for the actions whose ids start with <paramref name="prefix"/>: <c>issue</c> gives <c>Issue</c>.</summary>
    public static string GroupTitle(string prefix)
    {
        return prefix.Length == 0 ? prefix : string.Concat(char.ToUpperInvariant(prefix[0]).ToString(), prefix.AsSpan(1));
    }

    /// <summary>The rows without the blank separators and headers, for a popup too short to show them.</summary>
    public static IReadOnlyList<WhichKeyRow> WithoutHeaders(IReadOnlyList<WhichKeyRow> rows)
    {
        return rows.Where(row => !row.IsBlank && !row.IsHeader).ToList();
    }

    private static string PrefixOfRow(List<(string Prefix, WhichKeyRow Row)> plain, WhichKeyRow row)
    {
        return plain.First(entry => ReferenceEquals(entry.Row, row)).Prefix;
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

    /// <summary>The part of an action id before its first dot.</summary>
    public static string PrefixOf(string action)
    {
        var dot = action.IndexOf('.');

        return dot < 0 ? action : action[..dot];
    }

    /// <summary>
    /// The noun of a group: the last whole word that every action id under it shares (<c>issue.subtask-*</c> gives
    /// <c>sub-task</c>, so <c>Space t</c> and <c>Space i</c> do not both read "+issue").
    /// </summary>
    private static string Noun(
        KeymapTable keymap,
        InputMode mode,
        string pending,
        CommandLevel level,
        ActionContext? context)
    {
        var actions = new List<string>();

        CollectActions(keymap, mode, pending, level, context, actions);

        if (actions.Count == 0)
        {
            return NoActionsNoun;
        }

        var shared = SharedWords(actions);
        var start = shared.LastIndexOfAny(_wordSeparators) + 1;
        var noun = shared.Length == 0 ? PrefixOf(actions[0]) : shared[start..];

        return _nounText.GetValueOrDefault(noun, noun);
    }

    private static void CollectActions(
        KeymapTable keymap,
        InputMode mode,
        string pending,
        CommandLevel level,
        ActionContext? context,
        List<string> actions)
    {
        foreach (var next in keymap.NextKeys(mode, pending, level, context))
        {
            if (next.Action is null)
            {
                CollectActions(keymap, mode, $"{pending} {next.Key}", level, context, actions);
            }
            else
            {
                actions.Add(next.Action);
            }
        }
    }

    /// <summary>
    /// The longest start that all <paramref name="actions"/> share, cut back to whole words and without the trailing
    /// separator: <c>issue.subtask-assign</c> and <c>issue.subtask-transition</c> give <c>issue.subtask</c>.
    /// </summary>
    private static string SharedWords(List<string> actions)
    {
        var length = actions[0].Length;

        foreach (var action in actions)
        {
            length = Math.Min(length, action.Length);

            for (var index = 0; index < length; index++)
            {
                if (action[index] != actions[0][index])
                {
                    length = index;
                    break;
                }
            }
        }

        var common = actions[0][..length];
        var wholeWords = actions.All(action => action.Length == length || _wordSeparators.Contains(action[length]));

        if (wholeWords)
        {
            return common;
        }

        var cut = common.LastIndexOfAny(_wordSeparators);

        return cut < 0 ? string.Empty : common[..cut];
    }
}
