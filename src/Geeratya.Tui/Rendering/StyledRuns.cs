using Geeratya.Core.Text;
using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>Turns a styled line into runs a surface can draw, one style lookup per span.</summary>
public static class StyledRuns
{
    public static IReadOnlyList<StyledRun> Resolve(StyledLine line, ColorSupport tier)
    {
        var runs = new List<StyledRun>(line.Spans.Count);

        foreach (var span in line.Spans)
        {
            runs.Add(new StyledRun(span.Text, Palette.Resolve(tier, PaletteTable.For(span.Role))));
        }

        return runs;
    }
}
