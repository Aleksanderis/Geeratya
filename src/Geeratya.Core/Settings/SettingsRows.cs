using Geeratya.Core.State;

namespace Geeratya.Core.Settings;

/// <summary>What a row of the settings screen does on Enter.</summary>
public enum SettingsRowKind
{
    /// <summary>A setting of the registry: a toggle flips, a choice opens its list.</summary>
    Setting,

    /// <summary>The link to the profile manager.</summary>
    Profiles,

    /// <summary>A read-only line (a file path, the version).</summary>
    Info,
}

/// <summary>One row of the settings screen.</summary>
/// <param name="Category">The group the row is listed under.</param>
/// <param name="Kind">What Enter does.</param>
/// <param name="Label">The row's name.</param>
/// <param name="Value">The current value as the screen shows it.</param>
/// <param name="Help">One sentence for the help row.</param>
/// <param name="Setting">The registry entry of a <see cref="SettingsRowKind.Setting"/> row.</param>
public sealed record SettingsRow(
    string Category,
    SettingsRowKind Kind,
    string Label,
    string Value,
    string Help,
    SettingDefinition? Setting = null);

/// <summary>
/// The rows of the settings screen, generated from <see cref="SettingsRegistry"/> (a setting added there appears
/// here), then the link to the profile manager and the read-only file lines.
/// </summary>
public static class SettingsRows
{
    public const string ProfilesCategory = "Profiles";

    public const string AboutCategory = "About";

    private static readonly string[] _categoryOrder =
    [
        SettingsRegistry.AppearanceCategory,
        SettingsRegistry.HintsCategory,
        SettingsRegistry.StartupCategory,
    ];

    /// <summary>The rows in display order, grouped by category.</summary>
    public static IReadOnlyList<SettingsRow> Build(AppState state)
    {
        var rows = new List<SettingsRow>();
        var known = SettingsRegistry.All.Select(setting => setting.Category).Distinct().ToList();
        var order = _categoryOrder.Where(known.Contains).Concat(known.Except(_categoryOrder));

        foreach (var category in order)
        {
            rows.AddRange(SettingsRegistry.All
                .Where(setting => setting.Category == category)
                .Select(setting => ForSetting(state, setting)));
        }

        var active = state.Profiles.ActiveName ?? "none";

        rows.Add(new SettingsRow(
            ProfilesCategory,
            SettingsRowKind.Profiles,
            "profiles",
            active,
            "The sites and accounts Geeratya connects to. Enter opens the profile manager."));
        rows.Add(Info(
            "settings file",
            state.SettingsInfo.SettingsFile,
            "Where these settings are saved; comments in it survive a change. Edit it by hand if you like."));
        rows.Add(Info(
            "keymap file",
            state.SettingsInfo.KeymapFile,
            "Your key bindings. Edit it in your editor, then run :keys reload."));
        rows.Add(Info("version", ApplicationInfo.Version, "The version of this build."));

        return rows;
    }

    /// <summary>The category names in display order, each once.</summary>
    public static IReadOnlyList<string> Categories(IReadOnlyList<SettingsRow> rows)
    {
        return [.. rows.Select(row => row.Category).Distinct()];
    }

    /// <summary>The words a choice row offers: the theme names the host supplied, or the registry's list.</summary>
    public static IReadOnlyList<string> ChoicesOf(AppState state, SettingDefinition setting)
    {
        if (setting.Name == SettingsRegistry.ThemeName)
        {
            return [AppSettings.DefaultTheme, .. state.ThemeNames.Where(name => name != AppSettings.DefaultTheme)];
        }

        if (setting.Choices is { } choices)
        {
            return choices;
        }

        return setting.Kind == SettingKind.Toggle ? [SettingsRegistry.On, SettingsRegistry.Off] : [];
    }

    private static SettingsRow ForSetting(AppState state, SettingDefinition setting)
    {
        return new SettingsRow(
            setting.Category,
            SettingsRowKind.Setting,
            setting.Name,
            setting.Read(state.Settings),
            setting.Help,
            setting);
    }

    private static SettingsRow Info(string label, string value, string help)
    {
        var shown = value.Length == 0 ? "(unknown)" : value;

        return new SettingsRow(AboutCategory, SettingsRowKind.Info, label, shown, help);
    }
}
