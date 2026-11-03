namespace Geeratya.Core.Settings;

/// <summary>
/// One entry of the settings registry: the single place that says what a setting is called in <c>:set</c> and in the
/// file, its type, default and help. The reader, the writer, <c>:set</c> and the documentation table all use it.
/// </summary>
/// <param name="Name">The name in <c>:set</c>.</param>
/// <param name="ConfigKey">
/// The dotted path in <c>settings.json</c>: <c>hints.statusBar</c> is member <c>statusBar</c> of object <c>hints</c>.
/// </param>
/// <param name="Kind">The type of the value.</param>
/// <param name="Default">The default, spelled as <c>:set</c> shows it.</param>
/// <param name="Help">What the setting does, one sentence.</param>
/// <param name="Read">The current value, spelled as <c>:set</c> shows it.</param>
/// <param name="Apply">The settings with the typed value applied, or null when the value is not valid.</param>
/// <param name="Expected">What a valid value looks like, for the error text.</param>
/// <param name="LegacyConfigKey">The flat top-level key older files use; still read, never written.</param>
/// <param name="Aliases">Older <c>:set</c> names that still find the setting.</param>
public sealed record SettingDefinition(
    string Name,
    string ConfigKey,
    SettingKind Kind,
    string Default,
    string Help,
    Func<AppSettings, string> Read,
    Func<AppSettings, string, AppSettings?> Apply,
    string Expected,
    string? LegacyConfigKey = null,
    IReadOnlyList<string>? Aliases = null)
{
    /// <summary>The path segments of <see cref="ConfigKey"/>.</summary>
    public string[] ConfigPath => ConfigKey.Split('.');
}
