namespace Geeratya.Tui.Rendering;

/// <summary>
/// The themes a user can pick: the built-in ones and the <c>*.json</c> files of the <c>themes</c> folder of the
/// configuration directory. A built-in name wins over a file of the same name.
/// </summary>
public sealed class ThemeCatalog
{
    public const string FolderName = "themes";

    private const string Extension = ".json";

    private readonly string? _directory;

    public ThemeCatalog(string? directory)
    {
        _directory = directory;
    }

    /// <summary>Letters, digits, <c>-</c> and <c>_</c> only, so a name never leaves the themes folder.</summary>
    public static bool IsValidName(string? name)
    {
        return !string.IsNullOrEmpty(name) && name.All(IsNameCharacter);
    }

    /// <summary>Built-in names first, then the file names without extension, sorted. Never throws.</summary>
    public IReadOnlyList<string> Names()
    {
        var names = Themes.All.Select(theme => theme.Name).ToList();

        if (_directory is null || !Directory.Exists(_directory))
        {
            return names;
        }

        try
        {
            var files = Directory.EnumerateFiles(_directory, $"*{Extension}")
                .Select(path => Path.GetFileNameWithoutExtension(path))
                .Where(name => IsValidName(name) && Themes.Find(name) is null)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            names.AddRange(files);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return names;
        }

        return names;
    }

    /// <summary>The theme called <paramref name="name"/>, or the reasons it cannot be used. Never throws.</summary>
    public ThemeLoadResult Load(string name)
    {
        if (Themes.Find(name) is { } builtIn)
        {
            return new ThemeLoadResult(builtIn, []);
        }

        if (!IsValidName(name) || _directory is null)
        {
            return new ThemeLoadResult(null, [$"no theme called '{name}'"]);
        }

        var path = Path.Combine(_directory, $"{name}{Extension}");

        try
        {
            if (!File.Exists(path))
            {
                return new ThemeLoadResult(null, [$"no theme called '{name}'"]);
            }

            return ThemeFile.Parse(name, File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new ThemeLoadResult(null, [$"could not read {name}{Extension}: {exception.Message}"]);
        }
    }

    private static bool IsNameCharacter(char character)
    {
        return char.IsAsciiLetterOrDigit(character) || character is '-' or '_';
    }
}
