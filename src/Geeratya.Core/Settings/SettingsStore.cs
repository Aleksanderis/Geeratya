using System.Text.Json;

namespace Geeratya.Core.Settings;

/// <summary>Reads and writes <c>settings.json</c> in the configuration directory.</summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    private const string TemporarySuffix = ".tmp";

    private readonly string _path;

    public SettingsStore(string configurationDirectory)
    {
        _path = Path.Combine(configurationDirectory, FileName);
    }

    /// <summary>
    /// The saved settings; the defaults when the file is missing or unreadable (a bad settings file never stops the
    /// app and is left as it is). File system errors still throw.
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return AppSettings.Default;
        }

        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(_path), SettingsJsonContext.Default.AppSettings)
                ?? AppSettings.Default;
        }
        catch (JsonException)
        {
            return AppSettings.Default;
        }
    }

    /// <summary>Writes the settings through a temporary file, so a crash never leaves half a file.</summary>
    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{_path}{TemporarySuffix}";

        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings));
        File.Move(temporary, _path, overwrite: true);
    }
}
