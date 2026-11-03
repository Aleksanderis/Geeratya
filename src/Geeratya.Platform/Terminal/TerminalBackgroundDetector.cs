using Geeratya.Platform.Directories;

namespace Geeratya.Platform.Terminal;

/// <summary>
/// Decides whether the terminal background is light, for the <c>auto</c> theme: OSC 11 first (unless skipped), then
/// <c>COLORFGBG</c>; null when neither knows (the caller assumes dark). The query is skipped when not interactive, at
/// <c>NO_COLOR</c>, for <c>TERM</c> unset, <c>dumb</c>, <c>linux</c>, <c>ansi</c>, <c>cons25</c> or the <c>vt100</c> family, and on Windows (unsupported: conhost gives no
/// reliable answer, so only <c>COLORFGBG</c> counts there); Linux and macOS ask.
/// </summary>
public sealed class TerminalBackgroundDetector(
    EnvironmentReader environment,
    bool isInteractive,
    OperatingSystemKind operatingSystem,
    ITerminalBackgroundQuery query)
{
    /// <summary>How long a silent terminal is waited for.</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMilliseconds(50);

    /// <summary>
    /// True light, false dark, null unknown. <paramref name="allowQuery"/> false uses the environment only.
    /// </summary>
    public bool? IsLight(bool allowQuery)
    {
        if (allowQuery && CanAsk())
        {
            var answer = query.Ask(DefaultTimeout);

            if (answer is not null && BackgroundColors.ParseOsc11(answer) is { } light)
            {
                return light;
            }
        }

        return BackgroundColors.ParseColorFgBg(environment.Get("COLORFGBG"));
    }

    private static bool IsVtFamily(string term)
    {
        return term.Length > 2 && term.StartsWith("vt", StringComparison.Ordinal) && char.IsAsciiDigit(term[2]);
    }

    private bool CanAsk()
    {
        var term = environment.Get("TERM");

        return isInteractive
            && operatingSystem != OperatingSystemKind.Windows
            && environment.Get("NO_COLOR") is null
            && term is not (null or "dumb" or "linux" or "ansi" or "cons25")
            && !IsVtFamily(term);
    }
}
