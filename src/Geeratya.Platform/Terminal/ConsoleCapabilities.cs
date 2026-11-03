using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Terminal;

/// <summary>What the attached terminal can show, decided from the environment and the redirection state.</summary>
public sealed record ConsoleCapabilities(ColorSupport Colors, bool IsInteractive, GlyphSet Glyphs = GlyphSet.Unicode)
{
    /// <summary>
    /// Follows NO_COLOR, TERM and COLORTERM; redirected input or output means non-interactive. Classic consoles (the Linux
    /// virtual terminal, <c>TERM=linux</c>, and the Windows console host without Windows Terminal) get the ASCII glyph set and
    /// at most 16 colours: their fonts lack the symbols and they have no 24-bit colour.
    /// </summary>
    public static ConsoleCapabilities Detect(
        EnvironmentReader environment,
        bool isInputRedirected,
        bool isOutputRedirected,
        OperatingSystemKind operatingSystem = OperatingSystemKind.Linux)
    {
        var term = environment.Get("TERM");
        var isDumb = term == "dumb";
        var isInteractive = !isInputRedirected && !isOutputRedirected && !isDumb;
        var isClassic = IsClassicConsole(environment, operatingSystem, term);
        var glyphs = isDumb || isClassic || !IsUtf8(environment) ? GlyphSet.Ascii : GlyphSet.Unicode;

        if (environment.Get("NO_COLOR") is not null || isDumb || isOutputRedirected || HasNoColors(term))
        {
            return new ConsoleCapabilities(ColorSupport.None, isInteractive, glyphs);
        }

        var colors = !isClassic && HasTrueColor(environment, operatingSystem) ? ColorSupport.TrueColor : ColorSupport.Basic16;

        return new ConsoleCapabilities(colors, isInteractive, glyphs);
    }

    /// <summary>
    /// Linux: the virtual terminal (<c>TERM=linux</c>) and its relatives (<c>ansi</c>, <c>cons25</c>, the <c>vt</c> family).
    /// Windows: the console host, told apart by what it does not set: Windows Terminal sets <c>WT_SESSION</c>, other modern
    /// terminals (VS Code, mintty, ConEmu, Git Bash) set <c>TERM</c>, <c>TERM_PROGRAM</c>, <c>COLORTERM</c>, <c>ConEmuANSI</c>
    /// or <c>ANSICON</c>. macOS has no such console: Terminal.app and its peers keep the full look (a terminal's font
    /// cannot be asked).
    /// </summary>
    private static bool IsClassicConsole(EnvironmentReader environment, OperatingSystemKind operatingSystem, string? term)
    {
        return operatingSystem switch
        {
            OperatingSystemKind.Linux => IsVirtualTerminal(term),
            OperatingSystemKind.Windows => environment.Get("WT_SESSION") is null
                && term is null
                && environment.Get("TERM_PROGRAM") is null
                && environment.Get("COLORTERM") is null
                && environment.Get("ConEmuANSI") is null
                && environment.Get("ANSICON") is null,
            _ => false, // macOS: no classic console exists.
        };
    }

    private static bool IsVirtualTerminal(string? term)
    {
        return term is "linux" or "ansi" or "cons25" || IsVtFamily(term);
    }

    /// <summary>The VT100 family has no colours at all.</summary>
    private static bool HasNoColors(string? term)
    {
        return IsVtFamily(term);
    }

    private static bool IsVtFamily(string? term)
    {
        return term is not null && term.StartsWith("vt", StringComparison.Ordinal);
    }

    private static bool HasTrueColor(EnvironmentReader environment, OperatingSystemKind operatingSystem)
    {
        if (environment.Get("COLORTERM") is "truecolor" or "24bit")
        {
            return true;
        }

        // Windows Terminal does not set COLORTERM but supports 24-bit colour.
        return operatingSystem == OperatingSystemKind.Windows && environment.Get("WT_SESSION") is not null;
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
