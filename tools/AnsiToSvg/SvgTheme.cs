namespace Geeratya.AnsiToSvg;

/// <summary>The terminal's default colours, which cells without an explicit colour use.</summary>
public sealed record SvgTheme(int Foreground, int Background)
{
    public static SvgTheme Dark { get; } = new(0xd4d4d4, 0x1e1e1e);

    public static SvgTheme Light { get; } = new(0x1f2328, 0xffffff);

    public static SvgTheme? ByName(string name)
    {
        return name switch
        {
            "dark" => Dark,
            "light" => Light,
            _ => null
        };
    }
}
