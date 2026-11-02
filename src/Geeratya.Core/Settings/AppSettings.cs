namespace Geeratya.Core.Settings;

/// <summary>
/// The user's switches (<c>settings.json</c> in the configuration directory). Both hint helps are on by default.
/// </summary>
/// <param name="StatusHints">Show the key hints on the right of the status bar.</param>
/// <param name="WhichKey">Show the popup of the next keys while a multi-key binding is half typed.</param>
public sealed record AppSettings(bool StatusHints = true, bool WhichKey = true)
{
    public static AppSettings Default { get; } = new();
}
