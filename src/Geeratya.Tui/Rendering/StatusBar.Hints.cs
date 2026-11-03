using Geeratya.Core.Backlog;
using Geeratya.Core.Forms;
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
        new(InputMode.Normal, ActionIds.Quit, "quit"),
        new(InputMode.Help, ActionIds.NavigateDown, "down"),
        new(InputMode.Help, ActionIds.NavigateUp, "up"),
        new(InputMode.Help, ActionIds.HelpClose, "close"),
        new(InputMode.Command, ActionIds.PickerPrevious, "move", HintScope.Global, ActionIds.PickerNext),
        new(InputMode.Command, ActionIds.PaletteSubmit, "run"),
        new(InputMode.Command, ActionIds.Cancel, "cancel"),
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
        new(InputMode.Normal, ActionIds.NavigateDown, "scroll", HintScope.Detail, ActionIds.NavigateUp),
        new(InputMode.Normal, ActionIds.ItemOpen, "open", HintScope.Detail),
        new(InputMode.Normal, ActionIds.Cancel, "back", HintScope.Detail),
        new(InputMode.Normal, ActionIds.IssueOpenInBrowser, "browser", HintScope.Detail),
        new(InputMode.Normal, ActionIds.IssueOpenParent, "parent", HintScope.Detail),
        new(InputMode.Normal, ActionIds.CommentAdd, "comment", HintScope.Detail),
        new(InputMode.Normal, ActionIds.SectionNext, "pane", HintScope.Detail),
    ];

    /// <summary>The hints shown while the source picker is open (Insert mode, <see cref="AppState.Picker"/>).</summary>
    public static IReadOnlyList<StatusHint> PickerHints { get; } =
    [
        new(InputMode.Insert, ActionIds.PickerNext, "choose"),
        new(InputMode.Insert, ActionIds.FormNext, "open"),
        new(InputMode.Insert, ActionIds.Cancel, "close"),
    ];

    /// <summary>The hints shown while the profile manager is open (Insert mode, <see cref="AppState.ProfileList"/>).</summary>
    public static readonly IReadOnlyList<StatusHint> ManagerHints =
    [
        new(InputMode.Insert, ActionIds.FormNext, "switch"),
        new(InputMode.Insert, ActionIds.ProfileAdd, "add"),
        new(InputMode.Insert, ActionIds.ProfileEdit, "edit"),
        new(InputMode.Insert, ActionIds.ProfileRemove, "remove"),
        new(InputMode.Insert, ActionIds.Cancel, "close"),
    ];

    private const string SettingsText = "Settings: a change applies at once and is saved";

    /// <summary>The hints shown while the settings screen is open (Normal mode, <see cref="AppState.SettingsScreen"/>).</summary>
    public static readonly IReadOnlyList<StatusHint> SettingsHints =
    [
        new(InputMode.Normal, ActionIds.NavigateDown, "move", HintScope.Global, ActionIds.NavigateUp),
        new(InputMode.Normal, ActionIds.ItemOpen, "change"),
        new(InputMode.Normal, ActionIds.SettingsReset, "reset"),
        new(InputMode.Normal, ActionIds.Cancel, "close"),
    ];

    /// <summary>The hints shown while the comment editor is open (Insert mode, <see cref="AppState.Editor"/>).</summary>
    public static IReadOnlyList<StatusHint> EditorHints { get; } =
    [
        new(InputMode.Insert, ActionIds.FormSubmit, "save"),
        new(InputMode.Insert, ActionIds.Cancel, "cancel"),
    ];

    /// <summary>The hints of the profile-add prompt on <paramref name="field"/>; Enter's verb is the box footer's.</summary>
    public static IReadOnlyList<StatusHint> ProfileHints(ProfileFormField field)
    {
        var enter = new StatusHint(InputMode.Insert, ActionIds.FormNext, ProfileFormOverlay.AcceptVerb(field));
        var test = new StatusHint(InputMode.Insert, ActionIds.ProfileTest, "test");
        var cancel = new StatusHint(InputMode.Insert, ActionIds.Cancel, "cancel");

        if (field == ProfileFormField.Token)
        {
            return [enter, test, cancel];
        }

        return [enter, new StatusHint(InputMode.Insert, ActionIds.FormSubmit, "save"), test, cancel];
    }

    /// <summary>The hints shown while the help pages are open (not the Messages overlay, which has no pages).</summary>
    public static IReadOnlyList<StatusHint> HelpHints { get; } =
    [
        new(InputMode.Help, ActionIds.NavigateDown, "section"),
        new(InputMode.Help, ActionIds.HelpScrollDown, "scroll"),
        new(InputMode.Help, ActionIds.HelpFilter, "filter"),
        new(InputMode.Help, ActionIds.HelpClose, "close"),
    ];

    /// <summary>The hints shown while a field form is open (Insert mode, the active tab's form).</summary>
    public static IReadOnlyList<StatusHint> FormHints { get; } =
    [
        new(InputMode.Insert, ActionIds.FormFieldNext, "field", HintScope.Global, ActionIds.FormFieldPrevious),
        new(InputMode.Insert, ActionIds.FormSubmit, "save"),
        new(InputMode.Insert, ActionIds.Cancel, "cancel"),
    ];

    private static HintList HintTexts(AppState state, KeymapTable keymap)
    {
        var level = state.Settings.CommandLevel;

        if (state is { SettingsScreen: not null, Mode: InputMode.Normal })
        {
            return new HintList([], TextsOf(SettingsHints, state.Mode, HintScope.Global, keymap, level));
        }

        if (state.Editor is not null && state.Mode == InputMode.Insert)
        {
            return new HintList([], TextsOf(EditorHints, state.Mode, HintScope.Global, keymap, level));
        }

        if (FormReducer.IsOpen(state))
        {
            return new HintList([], TextsOf(FormHints, state.Mode, HintScope.Global, keymap, level));
        }

        if (state.Picker is not null || state.Jump is not null || state.ContextMenu is not null)
        {
            return new HintList([], TextsOf(PickerHints, state.Mode, HintScope.Global, keymap, level));
        }

        if (state is { Mode: InputMode.Help, MessagesOpen: false })
        {
            return new HintList([], TextsOf(HelpHints, state.Mode, HintScope.Global, keymap, level));
        }

        if (state is { Mode: InputMode.Insert, Form: { } profile })
        {
            return new HintList([], TextsOf(ProfileHints(profile.Field), state.Mode, HintScope.Global, keymap, level));
        }

        if (state is { Mode: InputMode.Insert, ProfileList: not null })
        {
            return new HintList([], TextsOf(ManagerHints, state.Mode, HintScope.Global, keymap, level));
        }

        if (state.Confirm?.Title is not null)
        {
            var close = KeyNotation.Display(keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmYes)) ?? string.Empty;

            return new HintList([], [new HintChip(close, "close")]);
        }

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
            return HintScope.Detail;
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
