using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>Builds the one-line status bar: mode badge and text on the left, key hints on the right.</summary>
public static class StatusBar
{
    private const string HintGap = "  ";
    private const string ConfirmSuffix = " (y/n)";

    public static IReadOnlyList<StatusHint> Hints { get; } =
    [
        new(InputMode.Normal, "?", "help", ActionIds.HelpToggle),
        new(InputMode.Normal, ":", "command", ActionIds.PaletteOpen),
        new(InputMode.Normal, "q", "quit", ActionIds.Quit),
        new(InputMode.Help, "j", "down", ActionIds.NavigateDown),
        new(InputMode.Help, "k", "up", ActionIds.NavigateUp),
        new(InputMode.Help, "Esc", "close", ActionIds.HelpClose),
        new(InputMode.Command, "Enter", "run", ActionIds.PaletteSubmit),
        new(InputMode.Command, "Esc", "cancel", ActionIds.Cancel),
        new(InputMode.Confirm, "y", "yes", ActionIds.ConfirmYes),
        new(InputMode.Confirm, "n", "no", ActionIds.ConfirmNo),
    ];

    /// <summary>The status line for <paramref name="state"/>, exactly <paramref name="width"/> columns wide.</summary>
    public static string Render(AppState state, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        var left = $" {Badge(state.Mode)} {Text(state, width)}".TrimEnd();
        var hints = string.Join(HintGap, Hints.Where(hint => hint.Mode == state.Mode).Select(Format));
        var gap = width - TextWidth.Of(left) - TextWidth.Of(hints);

        if (hints.Length == 0 || gap < HintGap.Length)
        {
            return TextWidth.Pad(left, width);
        }

        return $"{left}{new string(' ', gap)}{hints}";
    }

    private static string Format(StatusHint hint)
    {
        return $"{hint.Key} {hint.Label}";
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

    private static string Text(AppState state, int width)
    {
        return state.Mode switch
        {
            InputMode.Command => $":{state.PaletteText}",
            InputMode.Confirm => ConfirmText(state, width),
            InputMode.Help => string.Empty,
            _ => state.Message ?? state.PendingKeys,
        };
    }

    /// <summary>Cuts the question, never the answer keys: the suffix stays visible on a narrow terminal.</summary>
    private static string ConfirmText(AppState state, int width)
    {
        var used = TextWidth.Of($" {Badge(InputMode.Confirm)} {ConfirmSuffix}");
        var question = TextWidth.Fit(state.Confirm?.Message ?? string.Empty, Math.Max(0, width - used));

        return $"{question}{ConfirmSuffix}";
    }
}
