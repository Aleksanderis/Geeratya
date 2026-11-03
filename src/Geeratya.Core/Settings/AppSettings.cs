namespace Geeratya.Core.Settings;

/// <summary>
/// The user's switches (<c>settings.json</c> in the configuration directory). Both hint helps are on (hints full) by default.
/// </summary>
/// <param name="StatusHints">How much of the key hints the right of the status bar shows.</param>
/// <param name="WhichKey">Show the popup of the next keys while a multi-key binding is half typed.</param>
/// <param name="Theme">The name of the colour theme: a built-in one or a file in the <c>themes</c> folder.</param>
public sealed record AppSettings(HintLevel StatusHints = HintLevel.Full, bool WhichKey = true, string Theme = AppSettings.DefaultTheme)
{
    public const string DefaultTheme = "auto";

    public static AppSettings Default { get; } = new();
}
