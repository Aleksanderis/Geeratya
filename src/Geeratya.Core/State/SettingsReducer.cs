using Geeratya.Core.Settings;

namespace Geeratya.Core.State;

/// <summary>
/// <c>:set</c> lists every setting, <c>:set name</c> shows one and <c>:set name value</c> changes it. The names, values
/// and help come from <see cref="SettingsRegistry"/>.
/// </summary>
public static class SettingsReducer
{
    public const string Usage = "Usage: :set [name [value]]";

    public const string ThemeUsage = "Usage: :theme [name]";

    public static ReduceResult Palette(AppState state, string argument)
    {
        var parts = argument.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return Say(state, $"{SettingsRegistry.Describe(state.Settings)}. {Usage}");
        }

        if (parts.Length > 2 || SettingsRegistry.Find(parts[0]) is not { } setting)
        {
            return Say(state, Usage);
        }

        if (parts.Length == 1)
        {
            return Say(state, SettingsRegistry.Describe(setting, state.Settings));
        }

        return Change(state, setting, parts[1]);
    }

    /// <summary>
    /// <c>:theme [name]</c>: asks the host to switch (or list); the choice is saved once the host confirms.
    /// </summary>
    public static ReduceResult Theme(AppState state, string name)
    {
        return name.Contains(' ')
            ? Say(state, ThemeUsage)
            : new ReduceResult(state, [new ApplyThemeEffect(name)]);
    }

    /// <summary>The host's answer: a theme that is drawn becomes the saved choice.</summary>
    public static ReduceResult Applied(AppState state, ThemeApplied applied)
    {
        if (!applied.Applied)
        {
            return Say(state, applied.Message);
        }

        var changed = state.Settings with { Theme = applied.Name };

        return new ReduceResult(
            state with { Settings = changed, Message = applied.Message },
            [new SaveSettingsEffect(SettingsRegistry.Find(SettingsRegistry.ThemeName)!, changed)]);
    }

    /// <summary>The host found the settings file changed: its values apply and the change must be repeated.</summary>
    public static ReduceResult Reloaded(AppState state, SettingsReloaded reloaded)
    {
        return Say(state with { Settings = reloaded.Settings with { Theme = state.Settings.Theme } }, reloaded.Message);
    }

    private static ReduceResult Change(AppState state, SettingDefinition setting, string value)
    {
        if (setting.Name == SettingsRegistry.ThemeName)
        {
            return Theme(state, value);
        }

        if (setting.Apply(state.Settings, value) is not { } changed)
        {
            return Say(state, $"{setting.Name} takes {setting.Expected}.");
        }

        return new ReduceResult(
            state with { Settings = changed, Message = $"{setting.Name} {setting.Read(changed)}." },
            [new SaveSettingsEffect(setting, changed)]);
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, []);
    }
}
