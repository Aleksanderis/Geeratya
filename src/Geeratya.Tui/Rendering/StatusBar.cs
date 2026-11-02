using System.Globalization;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sources;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>Builds the one-line status bar: mode badge and text on the left, key hints on the right.</summary>
public static class StatusBar
{
    private const string HintGap = "  ";
    private const int FieldCount = 4;
    private const string PickerPrompt = "open a view";

    public static IReadOnlyList<StatusHint> Hints { get; } =
    [
        new(InputMode.Normal, ActionIds.HelpToggle, "help"),
        new(InputMode.Normal, ActionIds.PaletteOpen, "command"),
        new(InputMode.Normal, ActionIds.Quit, "quit"),
        new(InputMode.Help, ActionIds.NavigateDown, "down"),
        new(InputMode.Help, ActionIds.NavigateUp, "up"),
        new(InputMode.Help, ActionIds.HelpClose, "close"),
        new(InputMode.Command, ActionIds.PaletteSubmit, "run"),
        new(InputMode.Command, ActionIds.Cancel, "cancel"),
        new(InputMode.Confirm, ActionIds.ConfirmYes, "yes"),
        new(InputMode.Confirm, ActionIds.ConfirmNo, "no"),
        new(InputMode.Insert, ActionIds.FormNext, "next"),
        new(InputMode.Insert, ActionIds.Cancel, "cancel"),
    ];

    /// <summary>The hints shown while the source picker is open (Insert mode, <see cref="AppState.Picker"/>).</summary>
    public static IReadOnlyList<StatusHint> PickerHints { get; } =
    [
        new(InputMode.Insert, ActionIds.PickerNext, "choose"),
        new(InputMode.Insert, ActionIds.FormNext, "open"),
        new(InputMode.Insert, ActionIds.Cancel, "close"),
    ];

    /// <summary>The status line for <paramref name="state"/>, exactly <paramref name="width"/> columns wide.</summary>
    public static string Render(AppState state, int width, KeymapTable keymap)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var left = $" {Badge(state.Mode)} {Text(state, width, keymap)}".TrimEnd();
        var hints = string.Join(HintGap, HintTexts(state, keymap));
        var gap = width - TextWidth.Of(left) - TextWidth.Of(hints);

        if (hints.Length == 0 || gap < HintGap.Length)
        {
            return TextWidth.Pad(left, width);
        }

        return $"{left}{new string(' ', gap)}{hints}";
    }

    private static IReadOnlyList<string> HintTexts(AppState state, KeymapTable keymap)
    {
        return state.Picker is null ? HintsFor(state.Mode, keymap) : TextsOf(PickerHints, state.Mode, keymap);
    }

    /// <summary>
    /// The hints of <paramref name="mode"/> as <c>key label</c>, the key taken from the keymap; a hint whose action
    /// the keymap does not bind in that mode is left out.
    /// </summary>
    public static IReadOnlyList<string> HintsFor(InputMode mode, KeymapTable keymap)
    {
        return TextsOf(Hints, mode, keymap);
    }

    private static List<string> TextsOf(IReadOnlyList<StatusHint> hints, InputMode mode, KeymapTable keymap)
    {
        var texts = new List<string>();

        foreach (var hint in hints.Where(hint => hint.Mode == mode))
        {
            var keys = keymap.FindKeys(mode, hint.ActionId);

            if (keys is not null)
            {
                texts.Add($"{keys} {hint.Label}");
            }
        }

        return texts;
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

    private static string Text(AppState state, int width, KeymapTable keymap)
    {
        return state.Mode switch
        {
            InputMode.Command => $":{state.PaletteText}",
            InputMode.Confirm => ConfirmText(state, width, keymap),
            InputMode.Help => string.Empty,
            InputMode.Insert => state.Picker is null ? FormText(state.Form, width) : PickerText(state.Picker),
            _ => NormalText(state),
        };
    }

    private static string PickerText(SourcePickerState picker)
    {
        return picker.Notice ?? PickerPrompt;
    }

    /// <summary>A notice or pending keys win; otherwise the result of the connection check shows.</summary>
    private static string NormalText(AppState state)
    {
        if (state.Message is not null)
        {
            return state.Message;
        }

        return state.PendingKeys.Length > 0 ? state.PendingKeys : ConnectionText.Line(state.Session);
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

    /// <summary>Cuts the question, never the answer keys: the suffix stays visible on a narrow terminal.</summary>
    private static string ConfirmText(AppState state, int width, KeymapTable keymap)
    {
        var yes = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmYes);
        var no = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmNo);
        var suffix = $" ({yes}/{no})";
        var used = TextWidth.Of($" {Badge(InputMode.Confirm)} {suffix}");
        var question = TextWidth.Fit(state.Confirm?.Message ?? string.Empty, Math.Max(0, width - used));

        return $"{question}{suffix}";
    }
}
