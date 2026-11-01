using System.Text.Json;

namespace Geeratya.Core.Profiles;

/// <summary>Reads and writes the profiles file (<c>profiles.json</c> in the configuration directory).</summary>
public sealed class ProfileStore
{
    public const string FileName = "profiles.json";

    private const string CorruptSuffix = ".corrupt";
    private const string TemporarySuffix = ".tmp";

    private readonly string _path;

    public ProfileStore(string configurationDirectory)
    {
        _path = Path.Combine(configurationDirectory, FileName);
    }

    /// <summary>
    /// Loads the catalog. A missing file gives an empty catalog; an unreadable one gives an empty catalog, keeps
    /// a copy as <c>.corrupt</c> and reports <see cref="ProfileLoadStatus.Corrupt"/>. Never throws for bad content;
    /// file system errors (locked or read-only file) still throw.
    /// </summary>
    public ProfileLoadResult Load()
    {
        if (!File.Exists(_path))
        {
            return new ProfileLoadResult(ProfileCatalog.Empty, ProfileLoadStatus.Missing);
        }

        var catalog = TryParse();

        if (catalog is not null)
        {
            return new ProfileLoadResult(catalog, ProfileLoadStatus.Loaded);
        }

        File.Copy(_path, $"{_path}{CorruptSuffix}", overwrite: true);

        return new ProfileLoadResult(ProfileCatalog.Empty, ProfileLoadStatus.Corrupt);
    }

    /// <summary>Writes the catalog through a temporary file, so a crash never leaves half a file.</summary>
    public void Save(ProfileCatalog catalog)
    {
        var directory = Path.GetDirectoryName(_path);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporary = $"{_path}{TemporarySuffix}";

        File.WriteAllText(temporary, JsonSerializer.Serialize(catalog, ProfilesJsonContext.Default.ProfileCatalog));
        File.Move(temporary, _path, overwrite: true);
    }

    private ProfileCatalog? TryParse()
    {
        try
        {
            var catalog = JsonSerializer.Deserialize(
                File.ReadAllText(_path),
                ProfilesJsonContext.Default.ProfileCatalog);

            return IsValid(catalog) ? catalog : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool IsValid(ProfileCatalog? catalog)
    {
        return catalog is { Profiles: not null }
            && catalog.Profiles.All(IsFilled);
    }

    private static bool IsFilled(Profile? profile)
    {
        return profile is not null
            && !string.IsNullOrWhiteSpace(profile.Name)
            && !string.IsNullOrWhiteSpace(profile.SiteUrl)
            && !string.IsNullOrWhiteSpace(profile.Email);
    }
}
