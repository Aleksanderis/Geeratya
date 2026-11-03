using System.Globalization;
using Geeratya.Core.Backlog;
using Geeratya.Core.Comments;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Selection;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.Text;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>Builds the one-line status bar: mode badge and text on the left, key hints on the right.</summary>
public static class StatusBar
{
    private const string HintGap = "  ";
    private const string Separator = " · ";
    private const string PickerPrompt = "open a view";
    private const string SprintPrompt = "switch sprint";
    private const string MovePrompt = "move to sprint";
    private const string JumpPrompt = "jump to an issue";
    private const int MinViewRoom = 8;
    private const int MaxViewReserve = 40;

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
        return RenderStyled(state, width, keymap, now, confirmDialogShown).PlainText;
    }

    /// <summary>
    /// The status line as styled spans (<c>docs/ux-look.md</c>, section 5): the mode badge as a filled chip at column
    /// 0, the text (a message in its severity role), then the hint chips right-aligned, the key bold and the label
    /// muted. Its plain text is <see cref="Render"/>; it is exactly <paramref name="width"/> columns wide.
    /// </summary>
    public static StyledLine RenderStyled(
        AppState state,
        int width,
        KeymapTable keymap,
        DateTimeOffset? now = null,
        bool confirmDialogShown = false)
    {
        if (width <= 0)
        {
            return StyledLine.Empty;
        }

        var clock = now ?? DateTimeOffset.UnixEpoch;
        var text = Text(state, width, keymap, clock, confirmDialogShown, 0);
        var wanted = TextWidth.Of(Text(state, width, keymap, clock, confirmDialogShown, int.MaxValue))
            - TextWidth.Of(text);

        // The view text (source, count, filter and lane, breadcrumb, ruler) is state the user needs: up to
        // MaxViewReserve cells are kept for it and the screen hints drop from the end to make room (the global
        // hints stay while they fit).
        var reserve = Math.Min(Math.Max(0, wanted), MaxViewReserve);
        var left = Left(state, text);
        var room = width - TextWidth.Of(left);
        var hints = ShownHints(state, keymap, room - reserve);

        // The global hints never give way to the view text: shrink the reserve until they fit again.
        while (hints.Count == 0 && reserve > 0 && ShownHints(state, keymap, room).Count > 0)
        {
            reserve--;
            hints = ShownHints(state, keymap, room - reserve);
        }

        var hintWidth = HintBar.Width(hints);
        var spare = width - TextWidth.Of(left) - hintWidth - Separator.Length;

        if (hints.Count > 0)
        {
            spare -= HintGap.Length;
        }

        var shown = wanted > 0 && spare >= MinViewRoom
            ? Text(state, width, keymap, clock, confirmDialogShown, spare)
            : text;

        if (shown != text)
        {
            left = Left(state, shown);
        }

        var gap = width - TextWidth.Of(left) - hintWidth;
        var spans = new List<StyledSpan> { new($" {Badge(state)} ", BadgeRole(state)) };

        if (shown.TrimEnd().Length > 0)
        {
            spans.Add(new StyledSpan(" ", TextRole.BarText));
            spans.Add(new StyledSpan(shown.TrimEnd(), MessageRole(state, width, clock)));
        }

        if (hints.Count == 0 || gap < HintGap.Length)
        {
            return StyledLines.Fit(new StyledLine(spans), width, TextRole.BarText);
        }

        spans.Add(new StyledSpan(new string(' ', gap), TextRole.BarText));
        spans.AddRange(HintBar.Spans(hints));

        return StyledLines.Fit(new StyledLine(spans), width, TextRole.BarText);
    }

    private static string Left(AppState state, string text)
    {
        var chip = $" {Badge(state)} ";

        return text.TrimEnd().Length == 0 ? chip : $"{chip} {text.TrimEnd()}";
    }

    private static TextRole MessageRole(AppState state, int width, DateTimeOffset clock)
    {
        return MessageSegment(state, width, clock)?.Role switch
        {
            PaletteRole.Error => TextRole.BarError,
            PaletteRole.Warning => TextRole.BarWarning,
            PaletteRole.SuccessChip => TextRole.BarSuccessChip,
            PaletteRole.Success => TextRole.BarSuccess,
            _ => TextRole.BarText,
        };
    }

    /// <summary>
    /// The hints that fit in <paramref name="room"/>. The global ones are kept first; the screen hints before them are
    /// added in order while they fit (a gap to the text is kept), so a narrow line loses the screen hints from the
    /// end, one by one, and the global ones last.
    /// </summary>
    private static IReadOnlyList<HintChip> Fit(HintList hints, int room)
    {
        var used = HintBar.Width(hints.Global);

        if (used == 0 || used + HintGap.Length > room)
        {
            return [];
        }

        var screen = new List<HintChip>();

        foreach (var chip in hints.Screen)
        {
            var next = used + chip.Width + HintGap.Length;

            if (next + HintGap.Length > room)
            {
                break;
            }

            screen.Add(chip);
            used = next;
        }

        return [.. screen, .. hints.Global];
    }

    private static IReadOnlyList<HintChip> ShownHints(AppState state, KeymapTable keymap, int room)
    {
        if (state.Settings.StatusHints == HintLevel.Off)
        {
            return [];
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

    private readonly record struct HintList(IReadOnlyList<HintChip> Screen, IReadOnlyList<HintChip> Global);

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
            return HintScope.Columns;
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
        return hint == current || (hint == HintScope.List && current is HintScope.ListOnBoard or HintScope.Backlog);
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

    /// <summary>
    /// The message to paint in its severity role inside the bar, or null. Info is drawn in the success role and, for the
    /// first <see cref="StatusMessages.FreshFor"/> after it was posted, as a filled chip (bold reverse, so the
    /// no-colour tier shows it too); warnings and errors keep their roles.
    /// </summary>
    public static StatusSegment? MessageSegment(AppState state, int width, DateTimeOffset? now = null)
    {
        if (StatusMessages.Cells(state) is not { } cells || state.Status is not { } status || cells.Start >= width)
        {
            return null;
        }

        var role = status.Severity switch
        {
            StatusSeverity.Error => PaletteRole.Error,
            StatusSeverity.Warning => PaletteRole.Warning,
            _ => now is { } at && StatusMessages.IsFresh(state, at) ? PaletteRole.SuccessChip : PaletteRole.Success,
        };

        return new StatusSegment(cells.Start, Math.Min(cells.End, width) - cells.Start, status.Line, role);
    }

    /// <summary>
    /// The cells of the <c>Lane n/N: Name</c> text in the status line as drawn by <see cref="Render"/>, null when the
    /// line does not show it (no lane, or a message or typed keys instead); a cut line gives the part that shows.
    /// </summary>
    public static TabButton? LaneCells(
        AppState state,
        int width,
        KeymapTable keymap,
        DateTimeOffset? now = null,
        bool confirmDialogShown = false)
    {
        if (state.Mode != InputMode.Normal
            || ListBodyRenderer.ActiveTab(state) is not { Detail.Count: 0 } tab
            || BoardViewOptions.LaneLabel(tab) is not { } label)
        {
            return null;
        }

        var line = Render(state, width, keymap, now, confirmDialogShown);
        var index = line.IndexOf(label[..(label.IndexOf(':') + 1)], StringComparison.Ordinal);

        if (index < 0)
        {
            return null;
        }

        // A cut line (the name ends in an ellipsis) still shows the start of the label: that part is the chip.
        var shown = 0;

        while (shown < label.Length && index + shown < line.Length && line[index + shown] == label[shown])
        {
            shown++;
        }

        var start = TextWidth.Of(line[..index]);

        return new TabButton(start, start + TextWidth.Of(label[..shown]));
    }

    private static string Badge(AppState state)
    {
        if (state.MessagesOpen && state.Mode == InputMode.Help)
        {
            return "MESSAGES";
        }

        // Visual mode is a state of the tab over Normal mode (docs/ux.md, "Modes").
        var visual = state.Mode == InputMode.Normal && ListBodyRenderer.ActiveTab(state) is { Detail.Count: 0, VisualAnchor: not null };

        return visual ? "VISUAL" : Badge(state.Mode);
    }

    private static TextRole BadgeRole(AppState state)
    {
        if (state.MessagesOpen && state.Mode == InputMode.Help)
        {
            return TextRole.BadgeHelp;
        }

        var visual = state.Mode == InputMode.Normal && ListBodyRenderer.ActiveTab(state) is { Detail.Count: 0, VisualAnchor: not null };

        if (visual)
        {
            return TextRole.BadgeVisual;
        }

        return state.Mode switch
        {
            InputMode.Command => TextRole.BadgeCommand,
            InputMode.Help => TextRole.BadgeHelp,
            InputMode.Confirm => TextRole.BadgeConfirm,
            InputMode.Insert => TextRole.BadgeInsert,
            _ => TextRole.BadgeNormal,
        };
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
        bool confirmDialogShown,
        int viewRoom)
    {
        return state.Mode switch
        {
            InputMode.Command => $":{state.PaletteText}",
            InputMode.Confirm => ConfirmText(state, width, keymap, confirmDialogShown),
            InputMode.Help => string.Empty,
            InputMode.Insert when state.Editor is not null => EditorText(state.Editor),
            InputMode.Insert when FormReducer.OpenForm(state) is { } form => FormOverlay.StatusText(form),
            InputMode.Insert when state.Jump is { } jump => jump.Notice ?? JumpPrompt,
            InputMode.Insert when confirmDialogShown && state is { Picker: null, Form: { } profile } => ProfileFormOverlay.StatusText(profile),
            InputMode.Insert when state is { ProfileList: not null, Form: null } => ManagerText(state),
            InputMode.Insert => state.Picker is null ? FormText(state.Form, width) : PickerText(state.Picker),
            _ => NormalText(state, now, viewRoom),
        };
    }

    /// <summary>
    /// The profile manager's line: the notice when there is one, else only what is known for the active profile (its
    /// name and the result of its connection check); no request goes to an inactive site.
    /// </summary>
    private static string ManagerText(AppState state)
    {
        if (state.Status is { } status)
        {
            return status.Line;
        }

        if (state.Message is not null)
        {
            return state.Message;
        }

        if (state.Profiles.Active is not { } active)
        {
            return "No active profile";
        }

        var connection = ConnectionText.Line(state.Session);

        return connection.Length == 0 ? $"Active: {active.Name}" : $"Active: {active.Name}{Separator}{connection}";
    }

    private static string EditorText(CommentEditor editor)
    {
        var what = editor.IsEdit ? "Editing a comment on" : "Commenting on";
        var saving = editor.IsSaving ? " · saving…" : string.Empty;

        return $"{what} {editor.Key}{saving}";
    }

    private static string PickerText(SourcePickerState picker)
    {
        if (picker.Notice is { } notice)
        {
            return notice;
        }

        if (picker.Scope != PickerScope.Sprints)
        {
            return PickerPrompt;
        }

        return picker.Sprint?.MoveKeys is null ? SprintPrompt : MovePrompt;
    }

    /// <summary>
    /// Pending keys, a count and a rank pickup win (they echo what the user is typing); then the notice; otherwise the network word of the active tab (first, so it survives a cut)
    /// and the result of the connection check show.
    /// </summary>
    private static string NormalText(AppState state, DateTimeOffset now, int viewRoom)
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

        var word = NetworkBanner.StatusWord(TabStatus.Of(state.Session, now), now);
        var line = ConnectionText.Line(state.Session);
        var view = viewRoom > 0 ? ViewText(state, now, viewRoom) : string.Empty;

        if (view.Length > 0)
        {
            line = line.Length == 0 ? view : $"{view}{Separator}{line}";
        }

        if (ListBodyRenderer.ActiveTab(state) is { Detail.Count: 0 } selecting && RowSelection.Count(selecting) > 0)
        {
            // The network word stays first, so it survives a cut.
            return word.Length == 0 ? SelectedText(selecting) : $"{word}{Separator}{SelectedText(selecting)}";
        }

        if (word.Length == 0)
        {
            return line;
        }

        return line.Length == 0 ? word : $"{word}{Separator}{line}";
    }

    /// <summary>
    /// What the screen no longer draws as a title line of its own: the source and its count on a board, the
    /// breadcrumb and the ruler in the issue detail (<c>docs/ux-look.md</c>, sections 2 and 5).
    /// </summary>
    private static string ViewText(AppState state, DateTimeOffset now, int room)
    {
        if (ListBodyRenderer.ActiveTab(state) is not { } tab)
        {
            return string.Empty;
        }

        if (tab.Detail.Count > 0)
        {
            return CardText.Shorten(DetailStatus.Text(tab, room), room);
        }

        if (tab.Definition.Layout != ViewLayout.Columns)
        {
            return string.Empty;
        }

        return CardText.Shorten(ColumnsBodyRenderer.StatusText(tab, Glyphs.For(GlyphSet.Unicode), now), room);
    }

    private static string SelectedText(TabState tab)
    {
        return $"{RowSelection.Count(tab)} selected";
    }

    /// <summary>The typed count and keys of a multi-key binding: <c>3</c>, <c>g</c>, <c>3&lt;space&gt;k</c>.</summary>
    private static string PendingText(AppState state)
    {
        if (state.Count == 0)
        {
            return KeyNotation.Display(state.PendingKeys);
        }

        var count = state.Count.ToString(CultureInfo.InvariantCulture);

        return state.PendingKeys.Length == 0 ? count : $"{count}{KeyNotation.Display(state.PendingKeys)}";
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

        var step = $"[{(int)form.Field + 1}/{ProfileFormOverlay.FieldCount}]";

        if (form.Error is not null)
        {
            return $"{step} ! {form.Error}";
        }

        if (form.TestResult is not null)
        {
            return $"{step} {form.TestResult}";
        }

        var value = ProfileFormOverlay.Shown(form);
        var prefix = $"{step} {ProfileFormOverlay.Label(form.Field)}: ";
        var used = TextWidth.Of($" {Badge(InputMode.Insert)}  {prefix}_");

        return $"{prefix}{Tail(value, Math.Max(0, width - used))}_";
    }

    /// <summary>The end of <paramref name="text"/> that fits in <paramref name="width"/> columns, never splitting a character.</summary>
    internal static string Tail(string text, int width)
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
        var yes = KeyNotation.Display(keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmYes));
        var no = KeyNotation.Display(keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmNo));

        if (state.Confirm?.Title is { } title)
        {
            return $"{title}  ({yes}/{no} close)";
        }

        if (dialogShown)
        {
            return $"{ConfirmDialog.StatusText}  ({yes}/{no})";
        }

        var suffix = $" ({yes}/{no})";
        var used = TextWidth.Of($" {Badge(InputMode.Confirm)}  {suffix}");
        var question = TextWidth.Fit(state.Confirm?.Message ?? string.Empty, Math.Max(0, width - used));

        return $"{question}{suffix}";
    }
}
