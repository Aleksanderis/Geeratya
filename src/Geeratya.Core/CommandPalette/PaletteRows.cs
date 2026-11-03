using Geeratya.Core.Keymap;
using Geeratya.Core.Menus;
using Geeratya.Core.Sources;
using Geeratya.Core.State;

namespace Geeratya.Core.CommandPalette;

/// <summary>
/// The lines of the command palette (<c>docs/ux.md</c>, "Command palette"): every action that is not modal-only,
/// under group subheaders, filtered and ranked by the text typed after <c>:</c>. When the text starts with a command
/// name and a space it is a command line instead and the list is that command's usage. Pure.
/// </summary>
public static class PaletteRows
{
    private const string OtherGroup = "Other";
    private const string IssueGroup = "Issue";
    private const string KeySeparator = ", ";

    private static readonly string[] _extraCommandWords = ["q", "h", "browse", "layout"];

    // Actions that never belong in the list: the palette's own opener and the Esc action.
    private static readonly string[] _hiddenActions = [ActionIds.PaletteOpen, ActionIds.Cancel];

    // Commands that have no key and no action id of their own; they run as typed command lines.
    private static readonly PaletteOnly[] _paletteOnly =
    [
        new(PaletteCommands.Labels, "Edit the labels of the issue", IssueGroup),
        new(PaletteCommands.Priority, "Change the priority of the issue", IssueGroup),
    ];

    /// <summary>The list for the palette text of <paramref name="state"/>: subheaders and rows in display order.</summary>
    public static IReadOnlyList<PaletteRow> For(AppState state, KeymapTable keymap)
    {
        if (UsageOf(state.PaletteText) is { } usage)
        {
            return usage;
        }

        var filter = state.PaletteText.Trim();
        var focused = FocusedActions(state);
        var candidates = new List<Candidate>();

        foreach (var definition in ActionCatalog.All)
        {
            if (ActionCatalog.IsModalOnly(definition.Id) || _hiddenActions.Contains(definition.Id))
            {
                continue;
            }

            var row = new PaletteRow(
                PaletteRowKind.Action,
                definition.Id,
                definition.Title,
                KeysOf(keymap, definition.Id),
                !ActionCatalog.Unimplemented.Contains(definition.Id));
            var group = ActionCatalog.GroupOf(definition.Id) ?? OtherGroup;

            AddCandidate(candidates, filter, row, group, focused.Contains(definition.Id));
        }

        foreach (var only in _paletteOnly)
        {
            var row = new PaletteRow(PaletteRowKind.Command, only.Line, only.Title, string.Empty);

            AddCandidate(candidates, filter, row, only.Group, focused.Contains(ActionIds.IssueEdit));
        }

        return Arrange(candidates, filter.Length > 0);
    }

    /// <summary>The rows <c>Up</c>, <c>Down</c> and a click can land on, in display order.</summary>
    public static IReadOnlyList<PaletteRow> Selectable(AppState state, KeymapTable keymap)
    {
        return For(state, keymap).Where(row => row.IsSelectable).ToList();
    }

    /// <summary>
    /// True when the text is a command line: its first word is a command name (<c>jql</c>, <c>tab</c>, <c>q</c>,
    /// ...), typed alone or with arguments. Such text runs as a command and never as a row.
    /// </summary>
    public static bool IsCommandLine(string text)
    {
        var word = FirstWord(text.Trim());

        return PaletteCommands.All.Any(command => command.Name == word) || _extraCommandWords.Contains(word);
    }

    private static List<PaletteRow>? UsageOf(string text)
    {
        var name = FirstWord(text.Trim());
        var command = PaletteCommands.All.FirstOrDefault(candidate => candidate.Name == name);

        if (command is null)
        {
            return null;
        }

        return
        [
            new PaletteRow(PaletteRowKind.Usage, string.Empty, command.Usage, string.Empty),
            new PaletteRow(PaletteRowKind.Usage, string.Empty, command.Description, string.Empty),
        ];
    }

    private static string FirstWord(string text)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);

        return space < 0 ? text : text[..space];
    }

    private static HashSet<string> FocusedActions(AppState state)
    {
        var focused = new HashSet<string>();

        foreach (var menu in new[] { ContextMenus.For(state), ContextMenus.ForView(state) })
        {
            if (menu is null)
            {
                continue;
            }

            foreach (var item in menu.Items)
            {
                focused.Add(item.Action);
            }
        }

        return focused;
    }

    private static string KeysOf(KeymapTable keymap, string action)
    {
        var keys = keymap.Bindings
            .Where(binding => binding.Mode == InputMode.Normal && binding.Action == action)
            .Select(binding => binding.Keys)
            .Distinct();

        return string.Join(KeySeparator, keys);
    }

    private static void AddCandidate(List<Candidate> candidates, string filter, PaletteRow row, string group, bool focused)
    {
        if (PaletteFuzzy.Score(filter, row.Title, row.Id) is not { } score)
        {
            return;
        }

        candidates.Add(new Candidate(row, group, score, focused, candidates.Count));
    }

    // Groups in their fixed order, or the group with the best match first while filtering; inside a group the best
    // match first, then the focused view's actions, then catalog order.
    private static List<PaletteRow> Arrange(List<Candidate> candidates, bool filtering)
    {
        var order = ActionCatalog.Groups.Append(OtherGroup).ToList();
        var groups = candidates
            .GroupBy(candidate => candidate.Group)
            .OrderBy(group => filtering ? -group.Max(candidate => candidate.Score) : 0)
            .ThenBy(group => order.IndexOf(group.Key))
            .ToList();
        var rows = new List<PaletteRow>();

        foreach (var group in groups)
        {
            rows.Add(new PaletteRow(PaletteRowKind.Header, string.Empty, group.Key, string.Empty));

            rows.AddRange(group
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.Focused)
                .ThenBy(candidate => candidate.Order)
                .Select(candidate => candidate.Row));
        }

        return rows;
    }

    private sealed record Candidate(PaletteRow Row, string Group, int Score, bool Focused, int Order);

    private sealed record PaletteOnly(string Line, string Title, string Group);
}
