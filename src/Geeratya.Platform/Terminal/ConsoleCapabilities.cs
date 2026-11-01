namespace Geeratya.Platform.Terminal;

/// <summary>What the attached terminal can show, decided from the environment and the redirection state.</summary>
public sealed record ConsoleCapabilities(ColorSupport Colors, bool IsInteractive)
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

        if (environment.Get("NO_COLOR") is not null || isDumb || isOutputRedirected)
        {
            return new ConsoleCapabilities(ColorSupport.None, isInteractive);
        }

        var colorTerm = environment.Get("COLORTERM");
        var colors = colorTerm is "truecolor" or "24bit" ? ColorSupport.TrueColor : ColorSupport.Basic16;

        return new ConsoleCapabilities(colors, isInteractive);
    }
}
