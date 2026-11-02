namespace Geeratya.Platform.Terminal;

/// <summary>What the attached terminal can show, decided from the environment and the redirection state.</summary>
public sealed record ConsoleCapabilities(ColorSupport Colors, bool IsInteractive, GlyphSet Glyphs = GlyphSet.Unicode)
{
    /// <summary>Follows NO_COLOR, TERM=dumb and COLORTERM; redirected input or output means non-interactive.</summary>
    public static ConsoleCapabilities Detect(
        EnvironmentReader environment,
        bool isInputRedirected,
        bool isOutputRedirected)
    {
        var term = environment.Get("TERM");
        var isDumb = term == "dumb";
        var isInteractive = !isInputRedirected && !isOutputRedirected && !isDumb;
        var glyphs = isDumb || !IsUtf8(environment) ? GlyphSet.Ascii : GlyphSet.Unicode;

        if (environment.Get("NO_COLOR") is not null || isDumb || isOutputRedirected)
        {
            return new ConsoleCapabilities(ColorSupport.None, isInteractive, glyphs);
        }

        var colorTerm = environment.Get("COLORTERM");
        var colors = colorTerm is "truecolor" or "24bit" ? ColorSupport.TrueColor : ColorSupport.Basic16;

        return new ConsoleCapabilities(colors, isInteractive, glyphs);
    }

    /// <summary>
    /// The first of LC_ALL, LC_CTYPE and LANG that is set decides, as in the C library. An unset locale counts as
    /// Unicode: the terminal is real (ssh, containers often have no locale) and the app writes UTF-8 either way.
    /// </summary>
    private static bool IsUtf8(EnvironmentReader environment)
    {
        var locale = environment.Get("LC_ALL") ?? environment.Get("LC_CTYPE") ?? environment.Get("LANG");

        if (locale is null)
        {
            return true;
        }

        return locale.Contains("utf-8", StringComparison.OrdinalIgnoreCase)
            || locale.Contains("utf8", StringComparison.OrdinalIgnoreCase);
    }
}
