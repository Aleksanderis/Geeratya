using Geeratya.Core.Settings;

namespace Geeratya.Core.State;

/// <summary><c>:set hints|whichkey on|off</c>: flips a switch and asks for the settings file to be written.</summary>
public static class SettingsReducer
{
    public const string Usage = "Usage: :set hints|whichkey on|off";

    private const string HintsName = "hints";
    private const string WhichKeyName = "whichkey";

    public static ReduceResult Palette(AppState state, string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return Say(state, Describe(state.Settings));
        }

        if (parts.Length != 2 || ParseSwitch(parts[1]) is not { } on)
        {
            return Say(state, Usage);
        }

        var changed = Change(state.Settings, parts[0], on);

        if (changed is null)
        {
            return Say(state, Usage);
        }

        return new ReduceResult(
            state with { Settings = changed, Message = $"{parts[0]} {Word(on)}." },
            [new SaveSettingsEffect(changed)]);
    }

    private static AppSettings? Change(AppSettings settings, string name, bool on)
    {
        return name switch
        {
            HintsName => settings with { StatusHints = on },
            WhichKeyName => settings with { WhichKey = on },
            _ => null,
        };
    }

    private static bool? ParseSwitch(string text)
    {
        return text switch
        {
            "on" => true,
            "off" => false,
            _ => null,
        };
    }

    private static string Describe(AppSettings settings)
    {
        return $"hints {Word(settings.StatusHints)}, whichkey {Word(settings.WhichKey)}. {Usage}";
    }

    private static string Word(bool on)
    {
        return on ? "on" : "off";
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, []);
    }
}
