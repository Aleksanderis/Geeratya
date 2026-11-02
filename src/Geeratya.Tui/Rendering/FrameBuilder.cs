using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Lays the shell out for a terminal size: a header region, a body from the first <see cref="IBodyRenderer"/> that handles
/// the state, and the status bar. Pure, so it is snapshot-tested and printed by <c>--print-frame</c> (plain text, styles dropped).
/// </summary>
public static class FrameBuilder
{
    private static readonly IReadOnlyList<IBodyRenderer> _defaultRenderers = [new HelpBodyRenderer(), new ViewBodyRenderer(), new StartBodyRenderer()];

    /// <summary>
    /// Exactly <paramref name="height"/> lines of exactly <paramref name="width"/> columns; empty for a size of zero.
    /// </summary>
    public static IReadOnlyList<string> Build(
        AppState state,
        int width,
        int height,
        IReadOnlyList<string> helpLines,
        KeymapTable keymap)
    {
        return BuildStyled(state, width, height, helpLines, keymap)?.PlainLines() ?? [];
    }

    /// <summary>
    /// The styled frame; null for a size of zero. <paramref name="screens"/> are tried before the built-in help and
    /// start screens, in order. <paramref name="headerRows"/> is the header region (the tab bar slot): zero until
    /// tabs exist, so today's frames do not change; the tab bar issue passes one.
    /// </summary>
    public static StyledFrame? BuildStyled(
        AppState state,
        int width,
        int height,
        IReadOnlyList<string> helpLines,
        KeymapTable keymap,
        IReadOnlyList<IBodyRenderer>? screens = null,
        int headerRows = 0)
    {
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var header = Math.Min(headerRows, height - 1);
        var bodyHeight = height - 1 - header;
        var context = new BodyContext(helpLines, keymap);
        var renderer = screens?.Concat(_defaultRenderers).First(candidate => candidate.AppliesTo(state))
            ?? _defaultRenderers.First(candidate => candidate.AppliesTo(state));
        var body = renderer.Render(state, context, width, bodyHeight);

        return new StyledFrame(
            Fit([], width, header),
            Fit(body, width, bodyHeight),
            StatusBar.Render(state, width, keymap));
    }

    private static List<StyledLine> Fit(IEnumerable<StyledLine> lines, int width, int height)
    {
        var fitted = lines.Take(height).Select(line => StyledLines.Fit(line, width)).ToList();

        while (fitted.Count < height)
        {
            fitted.Add(StyledLines.Fit(StyledLine.Empty, width));
        }

        return fitted;
    }
}
