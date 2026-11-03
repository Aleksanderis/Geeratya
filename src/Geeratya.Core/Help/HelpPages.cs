using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Help;

/// <summary>
/// The pages of the help overlay (<c>docs/ux.md</c>, "Help"), generated from the keymap and the action catalog and
/// never written by hand: the keys of the active layout, Global, every action, the palette commands and, when a
/// <c>keymap.json</c> changed something, Custom. Pure.
/// </summary>
public static class HelpPages
{
    public const string GlobalName = "Global";
    public const string AllName = "All actions";
    public const string CommandsName = "Commands";
    public const string CustomName = "Custom";

    private const string DialogsGroup = "Dialogs";
    private const string RowIndent = "  ";
    private const int KeyColumnWidth = 10;
    private const string OthersSeparator = ", ";

    /// <summary>
    /// The page names in order: the active layout (when a tab is open), Global, All actions, Commands, Custom (when
    /// used).
    /// </summary>
    public static IReadOnlyList<string> Names(AppState state, KeymapTable keymap)
    {
        var names = new List<string>();

        if (LayoutScope(state) is { } layout)
        {
            names.Add(NameOf(layout));
        }

        names.Add(GlobalName);
        names.Add(AllName);
        names.Add(CommandsName);

        if (keymap.Custom.Count > 0)
        {
            names.Add(CustomName);
        }

        return names;
    }

    /// <summary>
    /// The index of the page that shows: <see cref="AppState.HelpPage"/> kept inside the pages that exist.
    /// </summary>
    public static int Current(AppState state, KeymapTable keymap)
    {
        return Math.Clamp(state.HelpPage, 0, Names(state, keymap).Count - 1);
    }

    /// <summary>The page after or before the shown one, wrapping around.</summary>
    public static int Step(AppState state, KeymapTable keymap, int direction)
    {
        var count = Names(state, keymap).Count;

        return ((Current(state, keymap) + direction) % count + count) % count;
    }

    /// <summary>
    /// The lines of the shown page, narrowed by the filter (a case-insensitive match on a key, the title or the action
    /// id). A page with nothing left says so.
    /// </summary>
    public static IReadOnlyList<string> Lines(AppState state, KeymapTable keymap)
    {
        var name = Names(state, keymap)[Current(state, keymap)];
        var filter = state.HelpFilter.Trim();
        var level = state.Settings.CommandLevel;
        var lines = name switch
        {
            GlobalName => ActionLines(keymap, level, filter, row => row.Scope == ActionScope.Global && !row.ModalOnly),
            AllName => AllLines(keymap, level, filter),
            CommandsName => CommandLines(filter),
            CustomName => CustomLines(keymap, filter),
            _ => ActionLines(keymap, level, filter, row => row.Applies(LayoutScope(state))),
        };

        if (lines.Count == 0)
        {
            return [$"{RowIndent}Nothing matches /{filter}"];
        }

        return lines;
    }

    /// <summary>
    /// The count of Normal mode bindings the level hides, as one help line naming the toggle key; null when none are
    /// hidden. The key is read from the keymap.
    /// </summary>
    public static string? HiddenNote(KeymapTable keymap, CommandLevel level)
    {
        var hidden = keymap.Bindings
            .Where(binding => binding.Mode == InputMode.Normal && !ActionCatalog.IsShownAt(binding.Action, level))
            .Select(binding => binding.Action)
            .Distinct()
            .Count();

        if (hidden == 0)
        {
            return null;
        }

        var noun = hidden == 1 ? "advanced command" : "advanced commands";
        var toggle = keymap.FindKeys(InputMode.Help, ActionIds.ViewCycleCommandLevel);
        var how = toggle is null ? "set commandlevel advanced shows them" : $"{toggle} shows them";

        return $"{RowIndent}({hidden} {noun} hidden; {how})";
    }

    private static ActionScope? LayoutScope(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        if (tab is null)
        {
            return null;
        }

        if (tab.Detail.Count > 0)
        {
            return ActionScope.Detail;
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ActionScope.Columns;
        }

        var isBacklog = tab.Definition.Source is ViewSourceKind.Backlog or ViewSourceKind.ScrumBacklog;

        return isBacklog ? ActionScope.Backlog : ActionScope.List;
    }

    private static string NameOf(ActionScope layout)
    {
        return layout switch
        {
            ActionScope.Detail => "Issue detail",
            ActionScope.Columns => "Columns",
            ActionScope.Backlog => "Backlog",
            _ => "List",
        };
    }

    private static List<string> ActionLines(
        KeymapTable keymap,
        CommandLevel level,
        string filter,
        Func<HelpRow, bool> belongs)
    {
        var lines = new List<string>();

        foreach (var row in Rows(keymap, level).Where(row => belongs(row) && row.Matches(filter)))
        {
            lines.Add(row.Line);
        }

        return lines;
    }

    private static List<string> AllLines(KeymapTable keymap, CommandLevel level, string filter)
    {
        var lines = new List<string>();
        var rows = Rows(keymap, level).Where(row => row.Matches(filter)).ToList();

        foreach (var group in ActionCatalog.Groups.Append(DialogsGroup))
        {
            var inGroup = rows.Where(row => row.Group == group).ToList();

            if (inGroup.Count == 0)
            {
                continue;
            }

            lines.Add(group);
            lines.AddRange(inGroup.Select(row => row.Line));
        }

        if (filter.Length == 0 && HiddenNote(keymap, level) is { } note)
        {
            lines.Add(note);
        }

        return lines;
    }

    private static List<string> CommandLines(string filter)
    {
        var lines = new List<string>();

        foreach (var command in PaletteCommands.All)
        {
            var matches = filter.Length == 0
                || command.Usage.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || command.Description.Contains(filter, StringComparison.OrdinalIgnoreCase);

            if (matches)
            {
                lines.Add($"{RowIndent}:{command.Usage}");
                lines.Add($"{RowIndent}    {command.Description}");
            }
        }

        return lines;
    }

    private static List<string> CustomLines(KeymapTable keymap, string filter)
    {
        var lines = new List<string>();

        foreach (var row in keymap.Custom)
        {
            var title = row.Action is null ? null : ActionCatalog.FindTitle(row.Action);
            var matches = filter.Length == 0
                || row.Keys.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || (row.Action?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                || (title?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

            if (!matches)
            {
                continue;
            }

            var modeName = row.Mode.ToString().ToLowerInvariant();
            var mode = row.Mode == InputMode.Normal ? string.Empty : $" [{modeName} mode]";

            lines.Add(row.Action is null
                ? $"{RowIndent}Unbound: {row.Keys}{mode}"
                : $"{RowIndent}{row.Keys,-KeyColumnWidth} {title} ({row.Action}){mode}");
        }

        return lines;
    }

    // One row per action that has a key and that the level shows, in catalog order.
    private static List<HelpRow> Rows(KeymapTable keymap, CommandLevel level)
    {
        var rows = new List<HelpRow>();

        foreach (var definition in ActionCatalog.All)
        {
            var keys = KeysOf(keymap, definition.Id);

            if (keys.Count == 0 || !ActionCatalog.IsShownAt(definition.Id, level))
            {
                continue;
            }

            var modalOnly = ActionCatalog.IsModalOnly(definition.Id);
            var group = modalOnly ? DialogsGroup : ActionCatalog.GroupOf(definition.Id) ?? DialogsGroup;

            rows.Add(new HelpRow(definition, keys, ActionCatalog.ScopeOf(definition.Id), modalOnly, group));
        }

        return rows;
    }

    // The Normal mode keys of the action, or every key it has when it is bound only in dialogs.
    private static List<string> KeysOf(KeymapTable keymap, string action)
    {
        var normal = keymap.Bindings
            .Where(binding => binding.Mode == InputMode.Normal && binding.Action == action)
            .Select(binding => binding.Keys)
            .Distinct()
            .ToList();

        if (normal.Count > 0)
        {
            return normal;
        }

        return
        [
            .. keymap.Bindings
                .Where(binding => binding.Action == action)
                .Select(binding => binding.Keys)
                .Distinct(),
        ];
    }

    private sealed record HelpRow(
        ActionDefinition Definition,
        IReadOnlyList<string> Keys,
        ActionScope Scope,
        bool ModalOnly,
        string Group)
    {
        public string Line
        {
            get
            {
                var others = Keys.Count > 1 ? $"  ({string.Join(OthersSeparator, Keys.Skip(1))})" : string.Empty;

                return $"{RowIndent}{Keys[0],-KeyColumnWidth} {Definition.Title}{others}";
            }
        }

        public bool Applies(ActionScope? layout)
        {
            return !ModalOnly && layout is { } flag && (Scope & flag) != 0;
        }

        public bool Matches(string filter)
        {
            return filter.Length == 0
                || Definition.Title.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || Definition.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                || Keys.Any(keys => keys.Contains(filter, StringComparison.OrdinalIgnoreCase));
        }
    }
}
