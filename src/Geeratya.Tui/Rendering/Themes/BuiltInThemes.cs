namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// The theme files shipped inside the binary (embedded resources of <c>Rendering/Themes/Files</c>, same format as a
/// user file, read without reflection). A theme is added by dropping its file there and listing its name in
/// <see cref="Names"/>; a test checks both agree and that every one passes the contrast rules.
/// </summary>
public static class BuiltInThemes
{
    public const string DefaultDarkName = "catppuccin-mocha";
    public const string DefaultLightName = "catppuccin-latte";
    public const string AutoName = "auto";

    private const string ResourcePrefix = "themes/";
    private const string ResourceSuffix = ".json";
    private const string DarkAlias = "dark";
    private const string LightAlias = "light";

    private static readonly Lazy<Theme> _default = new(() => Find(DefaultDarkName)!.Theme!);

    /// <summary>The names of the built-in themes, in listed order.</summary>
    public static IReadOnlyList<string> Names { get; } =
    [
        DefaultDarkName,
        "catppuccin-macchiato",
        "catppuccin-frappe",
        DefaultLightName,
        "high-contrast",
        "tokyo-night",
        "tokyo-night-storm",
        "tokyo-night-moon",
        "tokyo-night-day",
        "gruvbox-dark-hard",
        "gruvbox-dark-medium",
        "gruvbox-dark-soft",
        "gruvbox-light-hard",
        "gruvbox-light-medium",
        "gruvbox-light-soft",
        "dracula",
        "nord",
        "solarized-dark",
        "solarized-light",
        "rose-pine",
        "rose-pine-moon",
        "rose-pine-dawn",
    ];

    /// <summary>The names of all embedded theme files, whether listed in <see cref="Names"/> or not.</summary>
    public static IReadOnlyList<string> EmbeddedNames { get; } = [.. typeof(BuiltInThemes).Assembly
        .GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^ResourceSuffix.Length])
        .Order(StringComparer.Ordinal)];

    /// <summary>The theme used when nothing else is chosen (and when a saved choice cannot be used).</summary>
    public static Theme Default => _default.Value;

    /// <summary>The built-in name behind <c>dark</c> and <c>light</c>; any other name comes back unchanged.</summary>
    public static string ResolveAlias(string name)
    {
        return name switch
        {
            DarkAlias => DefaultDarkName,
            LightAlias => DefaultLightName,
            _ => name,
        };
    }

    /// <summary>True for <c>auto</c>: the theme follows the terminal background.</summary>
    public static bool IsAuto(string name)
    {
        return name == AutoName;
    }

    /// <summary>True for the names a user file may not take: the aliases and the reserved <c>auto</c>.</summary>
    public static bool IsReserved(string name)
    {
        return name is DarkAlias or LightAlias or AutoName;
    }

    /// <summary>The parsed built-in theme of that lower-case name, null when there is none.</summary>
    public static ThemeLoadResult? Find(string name)
    {
        using var stream = typeof(BuiltInThemes).Assembly
            .GetManifestResourceStream($"{ResourcePrefix}{ResolveAlias(name)}{ResourceSuffix}");

        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);

        return ThemeFileParser.Parse(ResolveAlias(name), reader.ReadToEnd());
    }
}
