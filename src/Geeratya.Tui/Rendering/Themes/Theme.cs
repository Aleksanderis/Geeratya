namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// One colour theme: the 24-bit colours of a theme file, the variant that picks the 16-colour table and the optional
/// 16-colour overrides. The palette entry of every role is worked out once, here.
/// </summary>
public sealed class Theme
{
    private readonly IReadOnlyDictionary<PaletteRole, PaletteEntry> _entries;

    public Theme(
        string name,
        ThemeVariant variant,
        IReadOnlyDictionary<ThemeColor, Rgb> colors,
        IReadOnlyDictionary<ThemeColor, Ansi16Color> ansi)
    {
        Name = name;
        Variant = variant;
        Colors = colors;
        Ansi = ansi;
        _entries = PaletteTable.Build(this);
    }

    /// <summary>The lower-case name: a built-in theme or the file name of a user theme.</summary>
    public string Name { get; }

    public ThemeVariant Variant { get; }

    public IReadOnlyDictionary<ThemeColor, Rgb> Colors { get; }

    /// <summary>The colours the theme maps to a 16-colour name itself (the rest comes from the variant).</summary>
    public IReadOnlyDictionary<ThemeColor, Ansi16Color> Ansi { get; }

    public PaletteEntry Entry(PaletteRole role)
    {
        return _entries[role];
    }
}
