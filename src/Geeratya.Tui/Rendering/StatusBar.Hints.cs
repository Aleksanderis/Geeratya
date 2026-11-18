using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

public static partial class StatusBar
{
    public static IReadOnlyList<StatusHint> Hints { get; } =
    [
        new(InputMode.Normal, ActionIds.HelpToggle, "help"),
        new(InputMode.Normal, ActionIds.PaletteOpen, "commands"),
        new(InputMode.Normal, ActionIds.NavigateClose, "back"),
        new(InputMode.Confirm, ActionIds.ConfirmYes, "yes"),
        new(InputMode.Confirm, ActionIds.ConfirmNo, "no"),
        new(InputMode.Insert, ActionIds.FormNext, "next"),
        new(InputMode.Insert, ActionIds.Cancel, "cancel"),
    ];

    /// <summary>
    /// The main keys of the active tab's screen (list, columns, issue detail), most important first: when the line is
    /// too narrow the last ones are dropped, and the global hints (<see cref="Hints"/>) are kept before any of them.
    /// </summary>
    public static IReadOnlyList<StatusHint> ScreenHints { get; } =
    [
        new(InputMode.Normal, ActionIds.NavigateDown, "move", HintScope.List, ActionIds.NavigateUp),
        new(InputMode.Normal, ActionIds.ItemOpen, "open", HintScope.List),
        new(InputMode.Normal, ActionIds.IssueOpenInBrowser, "browser", HintScope.List),
        new(InputMode.Normal, ActionIds.IssueCreate, "create", HintScope.List),
        new(InputMode.Normal, ActionIds.SectionNext, "pane", HintScope.Backlog),
        new(InputMode.Normal, ActionIds.ViewLayoutToggle, "columns", HintScope.ListOnBoard),
        new(InputMode.Normal, ActionIds.SectionToggle, "sub-tasks", HintScope.ColumnsSubtasks),
        new(InputMode.Normal, ActionIds.NavigateLeft, "column", HintScope.Columns, ActionIds.NavigateRight),
        new(InputMode.Normal, ActionIds.NavigateDown, "card", HintScope.Columns, ActionIds.NavigateUp),
        new(InputMode.Normal, ActionIds.ItemOpen, "open", HintScope.Columns),
        new(InputMode.Normal, ActionIds.ViewLayoutToggle, "list", HintScope.Columns),
        new(InputMode.Normal, ActionIds.IssueOpenInBrowser, "browser", HintScope.Columns),
        new(InputMode.Normal, ActionIds.IssueCreate, "create", HintScope.Columns),
        new(InputMode.Normal, ActionIds.NavigateDown, "field", HintScope.Detail, ActionIds.NavigateUp),
        new(InputMode.Normal, ActionIds.ItemOpen, "edit/open", HintScope.Detail),
        new(InputMode.Normal, ActionIds.Cancel, "back", HintScope.Detail),
        new(InputMode.Normal, ActionIds.IssueOpenInBrowser, "browser", HintScope.Detail),
        new(InputMode.Normal, ActionIds.IssueOpenParent, "parent", HintScope.Detail),
        new(InputMode.Normal, ActionIds.CommentAdd, "comment", HintScope.Detail),
        new(InputMode.Normal, ActionIds.PaneFocusDown, "pane", HintScope.Detail, ActionIds.PaneFocusUp),
        new(InputMode.Normal, ActionIds.PaneZoom, "zoom", HintScope.Detail),
        new(InputMode.Normal, ActionIds.NavigateDown, "field", HintScope.DetailZoomed, ActionIds.NavigateUp),
        new(InputMode.Normal, ActionIds.ItemOpen, "edit/open", HintScope.DetailZoomed),
        new(InputMode.Normal, ActionIds.PaneZoom, "unzoom", HintScope.DetailZoomed),
        new(InputMode.Normal, ActionIds.PaneFocusRight, "section", HintScope.DetailZoomed, ActionIds.PaneFocusLeft),
        new(InputMode.Normal, ActionIds.IssueOpenInBrowser, "browser", HintScope.DetailZoomed),
        new(InputMode.Normal, ActionIds.Cancel, "back", HintScope.DetailZoomed),
    ];

    private static HintList HintTexts(AppState state, KeymapTable keymap)
    {
        // Every overlay and dialog reads the chips its in-box footer is drawn from (SurfaceHints), so the two agree.
        if (SurfaceHints.Of(state, keymap) is { } surface)
        {
            var split = surface.Chips.Count - surface.Kept;

            return new HintList([.. surface.Chips.Take(split)], [.. surface.Chips.Skip(split)]);
        }

        var level = state.Settings.CommandLevel;
        var global = TextsOf(Hints, state.Mode, HintScope.Global, keymap, level);

        return state.Mode == InputMode.Normal
            ? new HintList(TextsOf(ScreenHints, InputMode.Normal, ScopeOf(state), keymap, level), global)
            : new HintList([], global);
    }

    /// <summary>The screen of the active tab; <see cref="HintScope.Global"/> without a tab (start screen).</summary>
    private static HintScope ScopeOf(AppState state)
    {
        var tab = ListBodyRenderer.ActiveTab(state);

        if (tab is null)
        {
            return HintScope.Global;
        }

        if (tab.Detail.Count > 0)
        {
            return tab.Detail[^1].Zoomed is null ? HintScope.Detail : HintScope.DetailZoomed;
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ColumnsNavigation.SelectedIssue(tab) is { } issue && (issue.IsListed || issue.Subtasks.Count > 0)
                ? HintScope.ColumnsSubtasks
                : HintScope.Columns;
        }

        if (BacklogSections.Applies(tab))
        {
            return HintScope.Backlog;
        }

        return tab.Definition.Source is ViewSourceKind.Board or ViewSourceKind.Sprint ? HintScope.ListOnBoard : HintScope.List;
    }

    /// <summary>
    /// The hints of <paramref name="mode"/> as <c>key label</c>, the key taken from the keymap; a hint whose action
    /// the keymap does not bind in that mode is left out.
    /// </summary>
    public static IReadOnlyList<string> HintsFor(InputMode mode, KeymapTable keymap)
    {
        return [.. TextsOf(Hints, mode, HintScope.Global, keymap).Select(chip => chip.Text)];
    }

    /// <summary>
    /// The screen hints of <paramref name="scope"/> as <c>key label</c> (the keys of a list tab also include those of
    /// a board's list, which adds the layout switch).
    /// </summary>
    public static IReadOnlyList<string> ScreenHintsFor(HintScope scope, KeymapTable keymap)
    {
        return [.. TextsOf(ScreenHints, InputMode.Normal, scope, keymap).Select(chip => chip.Text)];
    }

    private static List<HintChip> TextsOf(
        IReadOnlyList<StatusHint> hints,
        InputMode mode,
        HintScope scope,
        KeymapTable keymap,
        CommandLevel level = CommandLevel.Advanced)
    {
        var texts = new List<HintChip>();

        foreach (var hint in hints.Where(hint =>
            hint.Mode == mode && Applies(hint.Scope, scope) && IsShownAt(hint, level)))
        {
            var keys = KeysOf(hint, mode, keymap);

            if (keys is not null)
            {
                texts.Add(new HintChip(keys, hint.Label));
            }
        }

        return texts;
    }

    /// <summary>
    /// True unless every action the hint names is above <paramref name="level"/>: a hint stays while any of its
    /// actions is shown (a level hides, it never disables).
    /// </summary>
    public static bool IsShownAt(StatusHint hint, CommandLevel level)
    {
        return ActionCatalog.IsShownAt(hint.ActionId, level)
            || (hint.SecondActionId is { } second && ActionCatalog.IsShownAt(second, level));
    }

    private static bool Applies(HintScope hint, HintScope current)
    {
        return hint == current
            || (hint == HintScope.List && current is HintScope.ListOnBoard or HintScope.Backlog)
            || (hint == HintScope.Columns && current == HintScope.ColumnsSubtasks);
    }

    /// <summary>
    /// The key text of a hint: the first key of its action, or <c>j/k</c> of two for a pair; null when unbound.
    /// </summary>
    private static string? KeysOf(StatusHint hint, InputMode mode, KeymapTable keymap)
    {
        var keys = KeyNotation.Display(keymap.FindKeys(mode, hint.ActionId));

        if (keys is null || hint.SecondActionId is null)
        {
            return keys;
        }

        var second = KeyNotation.Display(keymap.FindKeys(mode, hint.SecondActionId));

        return second is null ? null : $"{keys}/{second}";
    }
}
