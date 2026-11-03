using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;
using Geeratya.Tui.Rendering.Themes;

namespace Geeratya.Tui.Rendering;

/// <summary>Turns a styled line into runs a surface can draw, one style lookup per span.</summary>
public static class StyledRuns
{
    /// <remarks>With <c>faintSupported</c> false (classic consoles) dimmed text at 16 colours is bright black instead of faint.</remarks>
    public static IReadOnlyList<StyledRun> Resolve(StyledLine line, Theme theme, ColorSupport tier, bool faintSupported = true)
    {
        var runs = new List<StyledRun>(line.Spans.Count);

        foreach (var span in line.Spans)
        {
            runs.Add(new StyledRun(span.Text, Palette.Resolve(theme, tier, PaletteTable.For(span.Role), span.Ground, faintSupported)));
        }

        return runs;
    }
}
