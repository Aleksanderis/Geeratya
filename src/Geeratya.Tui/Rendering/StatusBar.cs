using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>Builds the one-line status bar: mode badge and text on the left, key hints on the right.</summary>
public static class StatusBar
{
    private const string HintGap = "  ";

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
    ];

    /// <summary>The status line for <paramref name="state"/>, exactly <paramref name="width"/> columns wide.</summary>
    public static string Render(AppState state, int width, KeymapTable keymap)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var left = $" {Badge(state.Mode)} {Text(state, width, keymap)}".TrimEnd();
        var hints = string.Join(HintGap, HintsFor(state.Mode, keymap));
        var gap = width - TextWidth.Of(left) - TextWidth.Of(hints);

        if (hints.Length == 0 || gap < HintGap.Length)
        {
            return TextWidth.Pad(left, width);
        }

        return $"{left}{new string(' ', gap)}{hints}";
    }

    /// <summary>
    /// The hints of <paramref name="mode"/> as <c>key label</c>, the key taken from the keymap; a hint whose action
    /// the keymap does not bind in that mode is left out.
    /// </summary>
    public static IReadOnlyList<string> HintsFor(InputMode mode, KeymapTable keymap)
    {
        var texts = new List<string>();

        foreach (var hint in Hints.Where(hint => hint.Mode == mode))
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
            _ => state.Message ?? state.PendingKeys,
        };
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
