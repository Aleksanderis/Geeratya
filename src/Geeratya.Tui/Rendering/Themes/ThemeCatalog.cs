namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// The themes that can be chosen: the built-in ones and the user's files in the <c>themes</c> folder of the
/// configuration directory (<c>&lt;name&gt;.json</c>; a user file wins over a built-in theme of the same name). The
/// folder is read once, on first use; a file that cannot be used is not offered and its reasons are in
/// <see cref="Problems"/>. Edits to a file show after a restart.
/// </summary>
/// <param name="themesDirectory">The user's theme folder; it need not exist, null means none.</param>
public sealed class ThemeCatalog(string? themesDirectory)
{
    private const string FileFilter = "*.json";
    private const string FileExtension = ".json";

    private readonly List<string> _problems = [];
    private Dictionary<string, Theme>? _user;

    /// <summary>The theme names on offer: the built-in ones, then the user's, each once.</summary>
    public IReadOnlyList<string> Names =>
    [
        .. BuiltInThemes.Names,
        .. User().Keys.Where(name => !BuiltInThemes.Names.Contains(name)).Order(StringComparer.Ordinal),
    ];

    /// <summary>Why user theme files were not offered, one entry per reason, starting with the file name.</summary>
    public IReadOnlyList<string> Problems
    {
        get
        {
            User();

            return _problems;
        }
    }

    /// <summary>
    /// The theme of a name (any case; <c>dark</c> and <c>light</c> are the default dark and light built-ins), null when
    /// there is none. A built-in file that is invalid (a bug) comes back as a result without a theme.
    /// </summary>
    public ThemeLoadResult? Find(string name)
    {
        var key = BuiltInThemes.ResolveAlias(name.Trim().ToLowerInvariant());

        if (User().TryGetValue(key, out var user))
        {
            return new ThemeLoadResult(user, []);
        }

        return BuiltInThemes.Find(key);
    }

    private Dictionary<string, Theme> User()
    {
        if (_user is not null)
        {
            return _user;
        }

        var themes = new Dictionary<string, Theme>(StringComparer.Ordinal);

        foreach (var path in ListFiles())
        {
            var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

            if (BuiltInThemes.IsReserved(name))
            {
                _problems.Add($"{Path.GetFileName(path)}: the name '{name}' is reserved");
            }
            else if (name.Any(char.IsWhiteSpace))
            {
                _problems.Add($"{Path.GetFileName(path)}: the name must not contain spaces");
            }
            else if (!themes.ContainsKey(name) && Read(name, path) is { } theme)
            {
                themes[name] = theme;
            }
        }

        _user = themes;

        return themes;
    }

    private List<string> ListFiles()
    {
        var paths = new List<string>();

        if (themesDirectory is null)
        {
            return paths;
        }

        try
        {
            if (Directory.Exists(themesDirectory))
            {
                paths.AddRange(Directory.EnumerateFiles(themesDirectory, FileFilter)
                    .Where(path => path.EndsWith(FileExtension, StringComparison.OrdinalIgnoreCase)));
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _problems.Add($"themes folder cannot be read: {exception.Message}");
        }

        paths.Sort(StringComparer.Ordinal);

        return paths;
    }

    private Theme? Read(string name, string path)
    {
        string text;

        try
        {
            text = File.ReadAllText(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _problems.Add($"{Path.GetFileName(path)}: cannot be read ({exception.Message})");

            return null;
        }

        var result = ThemeFileParser.Parse(name, text);

        foreach (var error in result.Errors)
        {
            _problems.Add($"{Path.GetFileName(path)}: {error}");
        }

        return result.Theme;
    }
}
