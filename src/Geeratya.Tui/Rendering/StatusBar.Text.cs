using System.Globalization;
using Geeratya.Core.Comments;
using Geeratya.Core.Flash;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Selection;
using Geeratya.Core.Sources;
using Geeratya.Core.Text;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

public static partial class StatusBar
{
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

        // Visual mode is a state of the tab over Normal mode (docs/internal/ux.md, "Modes").
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
        int viewRoom,
        bool withPosition = false,
        bool withGoal = true)
    {
        return state.Mode switch
        {
            InputMode.Command => $":{state.PaletteText}",
            InputMode.Confirm => ConfirmText(state, width, keymap, confirmDialogShown),
            InputMode.Help => string.Empty,
            InputMode.Insert when state.Editor is not null => EditorText(state.Editor),
            InputMode.Insert when state.PendingKeys.Length > 0 && FormReducer.OpenForm(state) is not null
                => KeyNotation.Display(state.PendingKeys),
            InputMode.Insert when FormReducer.OpenForm(state) is { } form => FormOverlay.StatusText(form),
            InputMode.Insert when state.Jump is { } jump => jump.Notice ?? JumpPrompt,
            InputMode.Insert when state.FlashJump is { } flash => state.Message ?? FlashText(flash, width),
            InputMode.Insert when confirmDialogShown && state is { Picker: null, Form: { } profile } => ProfileFormOverlay.StatusText(profile),
            InputMode.Insert when state is { ProfileList: not null, Form: null } => ManagerText(state),
            InputMode.Insert => state.Picker is null ? FormText(state.Form, width) : PickerText(state.Picker),
            _ => NormalText(state, now, viewRoom, withPosition, withGoal),
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
    private static string NormalText(AppState state, DateTimeOffset now, int viewRoom, bool withPosition, bool withGoal)
    {
        if (state.PendingKeys.Length > 0 || state.Count > 0)
        {
            return PendingText(state);
        }

        if (state.SettingsScreen is not null)
        {
            return state.Status?.Line ?? state.Message ?? SettingsText;
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
        var line = state.Session.Connection is not Connected ? ConnectionText.Line(state.Session) : string.Empty;
        var view = viewRoom > 0 ? StatusViewText.Of(state, now, viewRoom, withGoal) : string.Empty;

        if (withPosition && ListBodyRenderer.ActiveTab(state) is { Detail.Count: 0 } positioned)
        {
            var position = SelectedIssueText.Text(positioned);

            if (position.Length > 0)
            {
                view = view.Length == 0 ? position : $"{position}{Separator}{view}";
            }
        }

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

        var prefix = $"{step} {ProfileFormOverlay.Label(form.Field)}: ";
        var used = TextWidth.Of($" {Badge(InputMode.Insert)}  {prefix}");
        var window = PromptWindow.Of(ProfileFormOverlay.PromptOf(form, form.Field), Math.Max(0, width - used));

        return $"{prefix}{window.Before}{window.Caret}{window.After}";
    }

    private static string FlashText(FlashJumpState flash, int width)
    {
        var used = TextWidth.Of($" {Badge(InputMode.Insert)}  {FlashPrompt}");
        var window = PromptWindow.Of(new PromptText(flash.Text, flash.Back), Math.Max(0, width - used));

        return $"{FlashPrompt}{window.Before}{window.Caret}{window.After}";
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
