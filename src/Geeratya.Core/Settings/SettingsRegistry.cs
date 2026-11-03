using Geeratya.Core.Keymap;

namespace Geeratya.Core.Settings;

/// <summary>Every setting the app knows. A new setting is one more entry here and a row in <c>docs/settings.md</c>.</summary>
public static class SettingsRegistry
{
    public const string On = "on";

    public const string Off = "off";

    public const string ThemeName = "theme";

    public const string AppearanceCategory = "Appearance";

    public const string HintsCategory = "Hints and keys";

    public const string StartupCategory = "Startup";

    /// <summary>The category of a setting that names none; listed after the others.</summary>
    public const string OtherCategory = "Other";

    private const string Full = "full";

    private const string Minimal = "minimal";

    private const string HintsExpected = "full, minimal or off";

    private const string ToggleExpected = "on or off";

    private const string CurrentWord = "current";

    private const string TabWord = "tab";

    private const string JumpExpected = "current or tab";

    private const string ThemeExpected = "a theme name";

    private const string Everyday = "everyday";

    private const string Advanced = "advanced";

    private const string LevelExpected = "everyday or advanced";

    public static IReadOnlyList<SettingDefinition> All { get; } =
    [
        new SettingDefinition(
            "hints.statusBar",
            "hints.statusBar",
            SettingKind.Choice,
            Full,
            "How many key hints the right of the status bar shows: all of them, only help, commands and quit, or none.",
            settings => HintWord(settings.StatusHints),
            ApplyHints,
            HintsExpected,
            "statusHints",
            ["hints"],
            [Full, Minimal, Off],
            HintsCategory),
        new SettingDefinition(
            "hints.whichKey",
            "hints.whichKey",
            SettingKind.Toggle,
            On,
            "Show the popup of the next keys while a multi-key binding is half typed.",
            settings => Word(settings.WhichKey),
            (settings, text) => ParseToggle(text) is { } on ? settings with { WhichKey = on } : null,
            ToggleExpected,
            "whichKey",
            ["whichkey"],
            Category: HintsCategory),
        new SettingDefinition(
            "commandlevel",
            "commandLevel",
            SettingKind.Choice,
            Everyday,
            "Which actions help, the palette, which-key and the hints list: everyday ones only, or advanced too.",
            settings => settings.CommandLevel == CommandLevel.Advanced ? Advanced : Everyday,
            ApplyLevel,
            LevelExpected,
            Choices: [Everyday, Advanced],
            Category: HintsCategory),
        new SettingDefinition(
            ThemeName,
            "theme",
            SettingKind.Text,
            AppSettings.DefaultTheme,
            "The colour theme: a built-in one or a file in the themes folder.",
            settings => settings.Theme,
            ApplyTheme,
            ThemeExpected,
            Category: AppearanceCategory),
        new SettingDefinition(
            "transparent",
            "transparent",
            SettingKind.Toggle,
            Off,
            "Keep the terminal's own background instead of the theme's page colour; text colours stay.",
            settings => Word(settings.TransparentBackground),
            (settings, text) => ParseToggle(text) is { } on ? settings with { TransparentBackground = on } : null,
            ToggleExpected,
            Category: AppearanceCategory),
        new SettingDefinition(
            "jump.target",
            "jump.target",
            SettingKind.Choice,
            CurrentWord,
            "Where the jump prompt (<space>g) opens an issue: on top of the current tab, or in a new tab.",
            settings => settings.JumpTarget == JumpTarget.Tab ? TabWord : CurrentWord,
            ApplyJumpTarget,
            JumpExpected,
            Choices: [CurrentWord, TabWord],
            Category: StartupCategory),
        new SettingDefinition(
            "startup.restore",
            "startup.restore",
            SettingKind.Toggle,
            On,
            "Reopen the tabs of the last run on start; off starts on the board picker every time.",
            settings => Word(settings.RestoreTabs),
            (settings, text) => ParseToggle(text) is { } on ? settings with { RestoreTabs = on } : null,
            ToggleExpected,
            Category: StartupCategory),
    ];

    public static SettingDefinition? Find(string name)
    {
        return All.FirstOrDefault(setting => Matches(setting, name));
    }

    private static bool Matches(SettingDefinition setting, string name)
    {
        return setting.Name.Equals(name, StringComparison.OrdinalIgnoreCase)
            || setting.Aliases?.Any(alias => alias.Equals(name, StringComparison.OrdinalIgnoreCase)) == true;
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

    /// <summary>One line per setting (name, value, default, help), for the Messages overlay.</summary>
    public static IReadOnlyList<string> ListLines(AppSettings settings)
    {
        return [.. All.Select(setting => $"{setting.Name} {setting.Read(settings)} (default {setting.Default}) - {setting.Help}")];
    }

    /// <summary>One setting with its value, default and accepted values, for <c>:set name</c> (the help text is in the <c>:set</c> overlay; the status line is one row).</summary>
    public static string Describe(SettingDefinition setting, AppSettings settings)
    {
        return $"{setting.Name} {setting.Read(settings)} (default {setting.Default}; {setting.Expected})";
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

    private static string HintWord(HintLevel level)
    {
        return level switch
        {
            HintLevel.Minimal => Minimal,
            HintLevel.Off => Off,
            _ => Full,
        };
    }

    /// <summary>
    /// <c>on</c> is accepted as <c>full</c>, so files and habits from before the levels keep working.
    /// </summary>
    private static AppSettings? ApplyHints(AppSettings settings, string text)
    {
        HintLevel? level = text switch
        {
            Full or On => HintLevel.Full,
            Minimal => HintLevel.Minimal,
            Off => HintLevel.Off,
            _ => null,
        };

        return level is { } value ? settings with { StatusHints = value } : null;
    }

    private static AppSettings? ApplyLevel(AppSettings settings, string text)
    {
        CommandLevel? level = text switch
        {
            Everyday => CommandLevel.Everyday,
            Advanced => CommandLevel.Advanced,
            _ => null,
        };

        return level is { } value ? settings with { CommandLevel = value } : null;
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

    private static AppSettings? ApplyJumpTarget(AppSettings settings, string text)
    {
        return text switch
        {
            CurrentWord => settings with { JumpTarget = JumpTarget.Current },
            TabWord => settings with { JumpTarget = JumpTarget.Tab },
            _ => null,
        };
    }

    private static AppSettings? ApplyTheme(AppSettings settings, string text)
    {
        return text.Length == 0 || text.Contains(' ') ? null : settings with { Theme = text };
    }
}
