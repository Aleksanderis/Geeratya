namespace Geeratya.Core.Keymap;

/// <summary>Reads the user's keymap file (<c>keymap.json</c> in the configuration directory).</summary>
public sealed class UserKeymapStore
{
    public const string FileName = "keymap.json";

    private readonly string _path;

    public UserKeymapStore(string configurationDirectory)
    {
        _path = Path.Combine(configurationDirectory, FileName);
    }

    /// <summary>The full path of the file (it may not exist yet).</summary>
    public string FilePath => _path;

    /// <summary>
    /// The defaults with the user's file merged in. A missing file is the defaults; a file that cannot be read or
    /// parsed is the defaults plus a problem. Never throws.
    /// </summary>
    public KeymapLoadResult Load(IReadOnlyList<KeyBinding> defaults)
    {
        try
        {
            return File.Exists(_path)
                ? UserKeymap.Merge(defaults, File.ReadAllText(_path))
                : new KeymapLoadResult(defaults, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new KeymapLoadResult(defaults, [$"could not read {FileName}: {exception.Message}"]);
        }
    }
}
