using System.Text;
using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Writes a styled frame as lines with ANSI escape sequences, resolving every span through the same palette the
/// shell draws with (<see cref="StyledRuns"/>), so <c>--print-frame --colors</c> shows what a terminal would. Pure.
/// </summary>
public static class AnsiFrameWriter
{
    private const string Escape = "\u001b[";
    private const string Reset = "\u001b[0m";
    private const int EightColors = 8;
    private const int ForegroundBase = 30;
    private const int BrightForegroundBase = 90;
    private const int BackgroundBase = 40;
    private const int BrightBackgroundBase = 100;

    // The terminal's colour index (black, red, green, yellow, blue, magenta, cyan, white, then bright) of each
    // Ansi16Color, which follows Terminal.Gui's order (black, blue, green, cyan, red, ...).
    private static readonly int[] _terminalIndex = [0, 4, 2, 6, 1, 5, 3, 7, 8, 12, 10, 14, 9, 13, 11, 15];

    /// <summary>
    /// Every line of the frame (header, body, status bar) as text with escape sequences; the no-colour tier writes
    /// the text attributes (bold, underline, reverse) only, as the shell does under <c>NO_COLOR</c>.
    /// </summary>
    public static IReadOnlyList<string> Write(StyledFrame frame, Theme theme, ColorSupport tier, bool faintSupported)
    {
        var lines = frame.Header.Concat(frame.Body).Append(frame.StatusStyled ?? StyledLine.Of(frame.StatusLine));

        return [.. lines.Select(line => WriteLine(line, theme, tier, faintSupported))];
    }

    private static string WriteLine(StyledLine line, Theme theme, ColorSupport tier, bool faintSupported)
    {
        var builder = new StringBuilder();

        foreach (var run in StyledRuns.Resolve(line, theme, tier, faintSupported))
        {
            if (run.Text.Length == 0)
            {
                continue;
            }

            builder.Append(Sequence(run.Style, tier)).Append(run.Text).Append(Reset);
        }

        return builder.ToString();
    }

    private static string Sequence(ResolvedStyle style, ColorSupport tier)
    {
        var codes = new List<string>();

        AddAttributes(codes, style.Style);

        if (tier == ColorSupport.TrueColor && style.Foreground is { } foreground && style.Background is { } background)
        {
            codes.Add($"38;2;{foreground.Red};{foreground.Green};{foreground.Blue}");
            codes.Add($"48;2;{background.Red};{background.Green};{background.Blue}");
        }
        else if (tier == ColorSupport.Basic16 && style.Foreground16 is { } named && style.Background16 is { } ground)
        {
            codes.Add(Code(_terminalIndex[(int)named], ForegroundBase, BrightForegroundBase));
            codes.Add(Code(_terminalIndex[(int)ground], BackgroundBase, BrightBackgroundBase));
        }

        return codes.Count == 0 ? string.Empty : $"{Escape}{string.Join(';', codes)}m";
    }

    private static void AddAttributes(List<string> codes, RoleStyle style)
    {
        AddIf(codes, style, RoleStyle.Bold, "1");
        AddIf(codes, style, RoleStyle.Faint, "2");
        AddIf(codes, style, RoleStyle.Italic, "3");
        AddIf(codes, style, RoleStyle.Underline, "4");
        AddIf(codes, style, RoleStyle.Reverse, "7");
    }

    private static void AddIf(List<string> codes, RoleStyle style, RoleStyle flag, string code)
    {
        if (style.HasFlag(flag))
        {
            codes.Add(code);
        }
    }

    private static string Code(int index, int normalBase, int brightBase)
    {
        var code = index < EightColors ? normalBase + index : brightBase + index - EightColors;

        return code.ToString();
    }
}
