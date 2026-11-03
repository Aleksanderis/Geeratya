namespace Geeratya.Tui.Rendering.Themes;

/// <summary>
/// One colour theme: the 24-bit colours of a theme file, the variant that picks the 16-colour table and the optional
/// 16-colour overrides. The palette entry of every role is worked out once, here.
/// </summary>
public sealed class Theme
{
    // The view behind an overlay is the page moved towards black (a scrim), so the box keeps the theme's own page
    // colour and stands out as the bright part (ux-look.md, section 6); a light page needs less than a dark one.
    private const double DarkScrim = 0.6;
    private const double LightScrim = 0.15;
    private const double DimStart = 0.3;
    private const double DimStep = 0.05;
    private const double DimTextContrast = 3.0;

    private static readonly Rgb _black = new(0, 0, 0);

    private readonly IReadOnlyDictionary<PaletteRole, PaletteEntry> _entries;
    private readonly Rgb? _overlayBackground;

    public Theme(
        string name,
        ThemeVariant variant,
        IReadOnlyDictionary<ThemeColor, Rgb> colors,
        IReadOnlyDictionary<ThemeColor, Ansi16Color> ansi,
        Rgb? overlayBackground = null,
        bool keepTerminalBackground = false)
    {
        _overlayBackground = overlayBackground;
        KeepTerminalBackground = keepTerminalBackground;
        Name = name;
        Variant = variant;
        Colors = colors;
        Ansi = ansi;
        _entries = PaletteTable.Build(this);
        PageBackground = colors[ThemeColor.BaseBackground];
        Surface = overlayBackground ?? PageBackground;
        ScrimBackground = Rgb.Blend(
            PageBackground,
            _black,
            variant == ThemeVariant.Light ? LightScrim : DarkScrim);
        DimAmount = ChooseDimAmount(colors, ScrimBackground);
    }

    /// <summary>
    /// The page and the cards keep the terminal's own background (the <c>transparentBackground</c> setting). Text
    /// roles keep the theme's colours, fills (selection, bars, chips) and the overlay box stay painted.
    /// </summary>
    public bool KeepTerminalBackground { get; }

    /// <summary>The same theme drawn with or without the terminal's own background under the page.</summary>
    public Theme WithTerminalBackground(bool keep)
    {
        return keep == KeepTerminalBackground ? this : new Theme(Name, Variant, Colors, Ansi, _overlayBackground, keep);
    }

    /// <summary>The page background (<see cref="ThemeColor.BaseBackground"/>).</summary>
    public Rgb PageBackground { get; }

    /// <summary>
    /// The fill of an overlay box: the optional <c>overlayBackground</c> of the theme file, else the page colour (the
    /// view behind the box is what gets darker, <see cref="ScrimBackground"/>).
    /// </summary>
    public Rgb Surface { get; }

    /// <summary>The page background as it is drawn behind an overlay: the page moved towards black.</summary>
    public Rgb ScrimBackground { get; }

    /// <summary>
    /// How far text behind an overlay is blended towards the scrim: 0.3, lowered until muted text keeps a contrast
    /// of 3.0 against it.
    /// </summary>
    public double DimAmount { get; }

    /// <summary>The colour of <paramref name="color"/> as it is drawn behind an overlay.</summary>
    public Rgb Dimmed(Rgb color)
    {
        return Rgb.Blend(color, ScrimBackground, DimAmount);
    }

    private static double ChooseDimAmount(IReadOnlyDictionary<ThemeColor, Rgb> colors, Rgb scrim)
    {
        var muted = colors[ThemeColor.Muted];

        for (var amount = DimStart; amount > 0; amount -= DimStep)
        {
            if (Contrast.Ratio(Rgb.Blend(muted, scrim, amount), scrim) >= DimTextContrast)
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
