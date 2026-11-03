using System.Globalization;
using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Selection;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

/// <summary>Builds the one-line status bar: mode badge and text on the left, key hints on the right.</summary>
public static class StatusBar
{
    private const string HintGap = "  ";
    private const string Separator = " · ";
    private const int FieldCount = 4;
    private const string PickerPrompt = "open a view";
    private const string JumpPrompt = "jump to an issue";

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
        new(InputMode.Normal, ActionIds.ViewLayoutToggle, "columns", HintScope.ListOnBoard),
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
    ];

    /// <summary>The hints shown while the source picker is open (Insert mode, <see cref="AppState.Picker"/>).</summary>
    public static IReadOnlyList<StatusHint> PickerHints { get; } =
    [
        new(InputMode.Insert, ActionIds.PickerNext, "choose"),
        new(InputMode.Insert, ActionIds.FormNext, "open"),
        new(InputMode.Insert, ActionIds.Cancel, "close"),
    ];

    /// <summary>The hints shown while the comment editor is open (Insert mode, <see cref="AppState.Editor"/>).</summary>
    public static IReadOnlyList<StatusHint> EditorHints { get; } =
    [
        new(InputMode.Insert, ActionIds.FormSubmit, "save"),
        new(InputMode.Insert, ActionIds.Cancel, "cancel"),
    ];

    /// <summary>The hints shown while the help pages are open (not the Messages overlay, which has no pages).</summary>
    public static IReadOnlyList<StatusHint> HelpHints { get; } =
    [
        new(InputMode.Help, ActionIds.HelpPageNext, "page"),
        new(InputMode.Help, ActionIds.HelpFilter, "filter"),
        new(InputMode.Help, ActionIds.NavigateDown, "down"),
        new(InputMode.Help, ActionIds.NavigateUp, "up"),
        new(InputMode.Help, ActionIds.HelpClose, "close"),
    ];

    /// <summary>The hints shown while a field form is open (Insert mode, the active tab's form).</summary>
    public static IReadOnlyList<StatusHint> FormHints { get; } =
    [
        new(InputMode.Insert, ActionIds.FormFieldNext, "field", HintScope.Global, ActionIds.FormFieldPrevious),
        new(InputMode.Insert, ActionIds.FormSubmit, "save"),
        new(InputMode.Insert, ActionIds.Cancel, "cancel"),
    ];

    /// <summary>
    /// The status line for <paramref name="state"/>, exactly <paramref name="width"/> columns wide.
    /// <paramref name="now"/> is the clock the network word counts from (a rate-limit wait); without it the epoch is used.
    /// <paramref name="confirmDialogShown"/> says the Confirm dialog is drawn, so the line keeps only the short
    /// <c>Confirm  (y/n)</c>; otherwise the question itself stays in the line.
    /// </summary>
    public static string Render(
        AppState state,
        int width,
        KeymapTable keymap,
        DateTimeOffset? now = null,
        bool confirmDialogShown = false)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var text = Text(state, width, keymap, now ?? DateTimeOffset.UnixEpoch, confirmDialogShown);
        var left = $" {Badge(state)} {text}".TrimEnd();
        var hints = ShownHints(state, keymap, width - TextWidth.Of(left));
        var gap = width - TextWidth.Of(left) - TextWidth.Of(hints);

        if (hints.Length == 0 || gap < HintGap.Length)
        {
            return TextWidth.Pad(left, width);
        }

        return $"{left}{new string(' ', gap)}{hints}";
    }

    /// <summary>
    /// The hints as one string. The global ones are kept first; the
    /// screen hints before them are added in order while they fit (a gap to the text is kept), so a narrow line loses
    /// the screen hints from the end, one by one, and the global ones last.
    /// </summary>
    private static string Fit(HintList hints, int room)
    {
        var global = string.Join(HintGap, hints.Global);
        var used = TextWidth.Of(global);

        if (used == 0 || used + HintGap.Length > room)
        {
            return string.Empty;
        }

        var screen = new List<string>();

        foreach (var text in hints.Screen)
        {
            var next = used + TextWidth.Of(text) + HintGap.Length;

            if (next + HintGap.Length > room)
            {
                break;
            }

            screen.Add(text);
            used = next;
        }

        return string.Join(HintGap, screen.Append(global));
    }

    private static string ShownHints(AppState state, KeymapTable keymap, int room)
    {
        if (state.Settings.StatusHints == HintLevel.Off)
        {
            return string.Empty;
        }

        return Fit(Leveled(HintTexts(state, keymap), state), room);
    }

    /// <summary>Minimal keeps only the three global hints of Normal mode (<c>? help  : commands  q quit</c>).</summary>
    private static HintList Leveled(HintList hints, AppState state)
    {
        if (state.Settings.StatusHints != HintLevel.Minimal || state.Mode != InputMode.Normal)
        {
            return hints;
        }

        return new HintList([], hints.Global);
    }

    private readonly record struct HintList(IReadOnlyList<string> Screen, IReadOnlyList<string> Global);

    private static HintList HintTexts(AppState state, KeymapTable keymap)
    {
        var level = state.Settings.CommandLevel;

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
            return HintScope.Columns;
        }

        return tab.Definition.Source == ViewSourceKind.Board ? HintScope.ListOnBoard : HintScope.List;
    }

    /// <summary>
    /// The hints of <paramref name="mode"/> as <c>key label</c>, the key taken from the keymap; a hint whose action
    /// the keymap does not bind in that mode is left out.
    /// </summary>
    public static IReadOnlyList<string> HintsFor(InputMode mode, KeymapTable keymap)
    {
        return TextsOf(Hints, mode, HintScope.Global, keymap);
    }

    /// <summary>
    /// The screen hints of <paramref name="scope"/> as <c>key label</c> (the keys of a list tab also include those of
    /// a board's list, which adds the layout switch).
    /// </summary>
    public static IReadOnlyList<string> ScreenHintsFor(HintScope scope, KeymapTable keymap)
    {
        return TextsOf(ScreenHints, InputMode.Normal, scope, keymap);
    }

    private static List<string> TextsOf(
        IReadOnlyList<StatusHint> hints,
        InputMode mode,
        HintScope scope,
        KeymapTable keymap,
        CommandLevel level = CommandLevel.Advanced)
    {
        var texts = new List<string>();

        foreach (var hint in hints.Where(hint =>
            hint.Mode == mode && Applies(hint.Scope, scope) && IsShownAt(hint, level)))
        {
            var keys = KeysOf(hint, mode, keymap);

            if (keys is not null)
            {
                texts.Add($"{keys} {hint.Label}");
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
        return hint == current || (hint == HintScope.List && current == HintScope.ListOnBoard);
    }

    /// <summary>
    /// The key text of a hint: the first key of its action, or <c>j/k</c> of two for a pair; null when unbound.
    /// </summary>
    private static string? KeysOf(StatusHint hint, InputMode mode, KeymapTable keymap)
    {
        var keys = keymap.FindKeys(mode, hint.ActionId);

        if (keys is null || hint.SecondActionId is null)
        {
            return keys;
        }

        var second = keymap.FindKeys(mode, hint.SecondActionId);

        return second is null ? null : $"{keys}/{second}";
    }

    /// <summary>
    /// The message to paint in its severity colour inside the bar (warnings and errors; an info message keeps the bar's
    /// own colours), or null.
    /// </summary>
    public static StatusSegment? MessageSegment(AppState state, int width)
    {
        if (StatusMessages.Cells(state) is not { } cells || state.Status is not { } status
            || status.Severity == StatusSeverity.Info || cells.Start >= width)
        {
            return null;
        }

        var role = status.Severity == StatusSeverity.Error ? PaletteRole.Error : PaletteRole.Warning;

        return new StatusSegment(cells.Start, Math.Min(cells.End, width) - cells.Start, status.Line, role);
    }

    private static string Badge(AppState state)
    {
        if (state.MessagesOpen && state.Mode == InputMode.Help)
        {
            return "MESSAGES";
        }

        // Visual mode is a state of the tab over Normal mode (docs/ux.md, "Modes").
        var visual = state.Mode == InputMode.Normal && ListBodyRenderer.ActiveTab(state) is { VisualAnchor: not null };

        return visual ? "VISUAL" : Badge(state.Mode);
    }

    private static string Badge(InputMode mode)
    {
        return mode switch
        {
            InputMode.Command => "COMMAND",
            InputMode.Help => "HELP",
            InputMode.Confirm => "CONFIRM",
            InputMode.Insert => "INSERT",
            _ => "NORMAL",
        };
    }

    private static string Text(
        AppState state,
        int width,
        KeymapTable keymap,
        DateTimeOffset now,
        bool confirmDialogShown)
    {
        return state.Mode switch
        {
            InputMode.Command => $":{state.PaletteText}",
            InputMode.Confirm => ConfirmText(state, width, keymap, confirmDialogShown),
            InputMode.Help => string.Empty,
            InputMode.Insert when state.Editor is not null => EditorText(state.Editor),
            InputMode.Insert when FormReducer.OpenForm(state) is { } form => FormOverlay.StatusText(form),
            InputMode.Insert when state.Jump is { } jump => jump.Notice ?? JumpPrompt,
            InputMode.Insert => state.Picker is null ? FormText(state.Form, width) : PickerText(state.Picker),
            _ => NormalText(state, now),
        };
    }

    private static string EditorText(CommentEditor editor)
    {
        var what = editor.IsEdit ? "Editing a comment on" : "Commenting on";
        var saving = editor.IsSaving ? " · saving…" : string.Empty;

        return $"{what} {editor.Key}{saving}";
    }

    private static string PickerText(SourcePickerState picker)
    {
        return picker.Notice ?? PickerPrompt;
    }

    /// <summary>
    /// Pending keys, a count and a rank pickup win (they echo what the user is typing); then the notice; otherwise the network word of the active tab (first, so it survives a cut)
    /// and the result of the connection check show.
    /// </summary>
    private static string NormalText(AppState state, DateTimeOffset now)
    {
        if (state.PendingKeys.Length > 0 || state.Count > 0)
        {
            return PendingText(state);
        }

        if (ListBodyRenderer.ActiveTab(state)?.RankPickup is { } picked)
        {
            return $"Picked up {picked}: move to its new place and press the rank-to key again (Esc cancels)";
        }

        if (state.Status is { } status)
        {
            return status.Line;
        }

        if (state.Message is not null)
        {
            return state.Message;
        }

        if (ListBodyRenderer.ActiveTab(state) is { Detail.Count: 0 } selecting && RowSelection.Count(selecting) > 0)
        {
            return SelectedText(selecting);
        }

        var word = NetworkBanner.StatusWord(TabStatus.Of(state.Session, now), now);
        var line = ConnectionText.Line(state.Session);

        if (word.Length == 0)
        {
            return line;
        }

        return line.Length == 0 ? word : $"{word}{Separator}{line}";
    }

    private static string SelectedText(TabState tab)
    {
        return $"{RowSelection.Count(tab)} selected";
    }

    /// <summary>The typed count and keys of a multi-key binding: <c>3</c>, <c>g</c>, <c>3 Space k</c>.</summary>
    private static string PendingText(AppState state)
    {
        if (state.Count == 0)
        {
            return state.PendingKeys;
        }

        var count = state.Count.ToString(CultureInfo.InvariantCulture);

        return state.PendingKeys.Length == 0 ? count : $"{count} {state.PendingKeys}";
    }

    /// <summary>
    /// The form prompt. An error replaces the typed value; a long value is clipped from the left so the end,
    /// where the user is typing, stays visible.
    /// </summary>
    private static string FormText(ProfileForm? form, int width)
    {
        if (form is null)
        {
            return string.Empty;
        }

        var step = $"[{(int)form.Field + 1}/{FieldCount}]";

        if (form.Error is not null)
        {
            return $"{step} ! {form.Error}";
        }

        var (label, value) = form.Field switch
        {
            ProfileFormField.Name => ("Profile name", form.Name),
            ProfileFormField.SiteUrl => ("Site URL", form.SiteUrl),
            ProfileFormField.Email => ("Account email", form.Email),
            _ => ("API token (hidden)", new string('*', form.Token.Length)),
        };
        var prefix = $"{step} {label}: ";
        var used = TextWidth.Of($" {Badge(InputMode.Insert)} {prefix}_");

        return $"{prefix}{Tail(value, Math.Max(0, width - used))}_";
    }

    /// <summary>The end of <paramref name="text"/> that fits in <paramref name="width"/> columns, never splitting a character.</summary>
    private static string Tail(string text, int width)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        var used = 0;
        var start = text.Length;

        for (var index = starts.Length - 1; index >= 0; index--)
        {
            var end = index == starts.Length - 1 ? text.Length : starts[index + 1];
            var elementWidth = TextWidth.Of(text[starts[index]..end]);

            if (used + elementWidth > width)
            {
                break;
            }

            used += elementWidth;
            start = starts[index];
        }

        return text[start..];
    }

    /// <summary>
    /// The short line while the dialog is open. Without a dialog (no room, or the size is not known yet) it cuts
    /// the question, never the answer keys: the suffix stays visible on a narrow terminal.
    /// </summary>
    private static string ConfirmText(AppState state, int width, KeymapTable keymap, bool dialogShown)
    {
        var yes = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmYes);
        var no = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmNo);

        if (dialogShown)
        {
            return $"{ConfirmDialog.StatusText}  ({yes}/{no})";
        }

        var suffix = $" ({yes}/{no})";
        var used = TextWidth.Of($" {Badge(InputMode.Confirm)} {suffix}");
        var question = TextWidth.Fit(state.Confirm?.Message ?? string.Empty, Math.Max(0, width - used));

        return $"{question}{suffix}";
    }
}
