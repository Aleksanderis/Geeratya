namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// One colour theme: the 24-bit colours of a theme file, the variant that picks the 16-colour table and the optional
/// 16-colour overrides. The palette entry of every role is worked out once, here.
/// </summary>
public sealed class Theme
{
    // The overlay fill is the selection step moved a third of the way to the page; a step that would be too
    // close to the page to read as raised is moved less (ux-look.md, section 7).
    private const double SurfaceContrast = 1.3;
    private const double DimStart = 0.3;
    private const double DimStep = 0.05;
    private const double DimTextContrast = 3.0;

    private static readonly double[] _surfaceAmounts = [1.0 / 3, 0.2, 0.1];

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
        PageBackground = colors[ThemeColor.BaseBackground];
        Surface = ChooseSurface(colors);
        DimAmount = ChooseDimAmount(colors);
    }

    /// <summary>The page background (<see cref="ThemeColor.BaseBackground"/>).</summary>
    public Rgb PageBackground { get; }

    /// <summary>The fill of an overlay box: raised from the page, but quieter than the selection tint.</summary>
    public Rgb Surface { get; }

    /// <summary>
    /// How far text behind an overlay is blended towards the page background: 0.3, lowered until muted text keeps a
    /// contrast of 3.0 against the page.
    /// </summary>
    public double DimAmount { get; }

    /// <summary>The colour of <paramref name="color"/> as it is drawn behind an overlay.</summary>
    public Rgb Dimmed(Rgb color)
    {
        return Rgb.Blend(color, PageBackground, DimAmount);
    }

    private static Rgb ChooseSurface(IReadOnlyDictionary<ThemeColor, Rgb> colors)
    {
        var selection = colors[ThemeColor.Selection];
        var page = colors[ThemeColor.BaseBackground];

        foreach (var amount in _surfaceAmounts)
        {
            var candidate = Rgb.Blend(selection, page, amount);

            if (Contrast.Ratio(candidate, page) >= SurfaceContrast)
            {
                return candidate;
            }
        }

        return selection;
    }

    private static double ChooseDimAmount(IReadOnlyDictionary<ThemeColor, Rgb> colors)
    {
        var muted = colors[ThemeColor.Muted];
        var page = colors[ThemeColor.BaseBackground];

        for (var amount = DimStart; amount > 0; amount -= DimStep)
        {
            if (Contrast.Ratio(Rgb.Blend(muted, page, amount), page) >= DimTextContrast)
            {
                return amount;
            }
        }

        return 0;
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
