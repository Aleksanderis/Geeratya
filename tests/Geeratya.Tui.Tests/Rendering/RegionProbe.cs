using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Tui.Rendering;

namespace Geeratya.Tui.Tests.Rendering;

/// <summary>Reads the clickable rows of a drawn frame and clicks them through the real resolver and reducer.</summary>
internal static class RegionProbe
{
    private static readonly DateTimeOffset _at = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    /// <summary>For each region of the target: its index and the plain text of the screen row it covers.</summary>
    public static List<RegionRow> Rows(StyledFrame frame, MouseTarget target)
    {
        var lines = frame.PlainLines();

        return [.. frame.Regions.Where(region => region.Target == target)
            .Select(region => new RegionRow(region.Index, lines[region.Top], region))];
    }

    /// <summary>Clicks the middle of the region and reduces the command that comes out (null: nothing is bound).</summary>
    public static ReduceResult? Click(AppState state, StyledFrame frame, OverlayRegion region, MouseGesture gesture)
    {
        var height = frame.Header.Count + frame.Body.Count + 1;
        var width = frame.Body[0].PlainText.Length;
        var context = MouseContext.From(state, width, height, frame.Header.Count, _at, frame.Regions);
        var column = region.Left + region.Width / 2;
        var commands = MouseResolver.Resolve(context, column, region.Top, gesture, _at);

        return commands.Count == 0 ? null : new AppReducer(DefaultKeymap.Load()).Reduce(state, commands[0]);
    }

    internal sealed record RegionRow(int Index, string Text, OverlayRegion Region);
}
