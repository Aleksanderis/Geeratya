namespace Geeratya.Core.Settings;

/// <summary>Every setting the app knows. A new setting is one more entry here and a row in <c>docs/settings.md</c>.</summary>
public static class SettingsRegistry
{
    public const string On = "on";

    public const string Off = "off";

    public const string ThemeName = "theme";

    private const string ToggleExpected = "on or off";

    private const string ThemeExpected = "a theme name";

    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new SettingDefinition(
            "hints",
            "statusHints",
            SettingKind.Toggle,
            On,
            "Show the key hints on the right of the status bar.",
            settings => Word(settings.StatusHints),
            (settings, text) => ParseToggle(text) is { } on ? settings with { StatusHints = on } : null,
            ToggleExpected),
        new SettingDefinition(
            "whichkey",
            "whichKey",
            SettingKind.Toggle,
            On,
            "Show the popup of the next keys while a multi-key binding is half typed.",
            settings => Word(settings.WhichKey),
            (settings, text) => ParseToggle(text) is { } on ? settings with { WhichKey = on } : null,
            ToggleExpected),
        new SettingDefinition(
            ThemeName,
            "theme",
            SettingKind.Text,
            AppSettings.DefaultTheme,
            "The colour theme: a built-in one or a file in the themes folder.",
            settings => settings.Theme,
            ApplyTheme,
            ThemeExpected),
    ];

    public static SettingDefinition? Find(string name)
    {
        return All.FirstOrDefault(setting => setting.Name == name);
    }

    /// <summary>The value of the setting as the file holds it: a boolean or a quoted string.</summary>
    public static string ToJson(SettingDefinition setting, AppSettings settings)
    {
        var value = setting.Read(settings);

        if (setting.Kind == SettingKind.Toggle)
        {
            return value == On ? "true" : "false";
        }

        return SettingsJson.Quote(value);
    }

    /// <summary>Every setting with its value and default, for <c>:set</c> without an argument.</summary>
    public static string Describe(AppSettings settings)
    {
        return string.Join(
            ", ",
            All.Select(setting => $"{setting.Name} {setting.Read(settings)} (default {setting.Default})"));
    }

    /// <summary>One setting with its value, default, accepted values and help, for <c>:set name</c>.</summary>
    public static string Describe(SettingDefinition setting, AppSettings settings)
    {
        return $"{setting.Name} {setting.Read(settings)} (default {setting.Default}; {setting.Expected}). {setting.Help}";
    }

    /// <summary>The Markdown table in <c>docs/settings.md</c>.</summary>
    public static string DocumentationTable()
    {
        var rows = All.Select(setting =>
            $"| `{setting.Name}` | `{setting.ConfigKey}` | {setting.Expected} | `{setting.Default}` | {setting.Help} |");

        return string.Join(
            "\n",
            ["| `:set` name | File key | Values | Default | What it does |", "|---|---|---|---|---|", .. rows]);
    }

    private static string Word(bool on)
    {
        return on ? On : Off;
    }

    private static bool? ParseToggle(string text)
    {
        return text switch
        {
            On => true,
            Off => false,
            _ => null,
        };
    }

    private static AppSettings? ApplyTheme(AppSettings settings, string text)
    {
        return text.Length == 0 || text.Contains(' ') ? null : settings with { Theme = text };
    }
}
