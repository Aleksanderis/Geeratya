using Geeratya.Core.Keymap;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Lays the shell out as plain text lines for a terminal size; pure, so it is snapshot-tested and printed by <c>--print-frame</c>.
/// </summary>
public static class FrameBuilder
{
    private const string HelpTitle = "Help";
    private const string Title = "Geeratya";
    private const int BlockHeight = 3;
    private const string StartHintGap = "   ";

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
        if (width <= 0 || height <= 0)
        {
            return [];
        }

        var bodyHeight = height - 1;
        var body = state.Mode == InputMode.Help
            ? HelpBody(state, helpLines, width, bodyHeight)
            : StartBody(width, bodyHeight, string.Join(StartHintGap, StatusBar.HintsFor(InputMode.Normal, keymap)));

        var lines = new List<string>(body) { StatusBar.Render(state, width, keymap) };

        return lines;
    }

    private static List<string> HelpBody(AppState state, IReadOnlyList<string> helpLines, int width, int bodyHeight)
    {
        var lines = new List<string> { HelpTitle };

        lines.AddRange(helpLines.Skip(state.HelpScroll));

        return Normalize(lines, width, bodyHeight);
    }

    private static List<string> StartBody(int width, int bodyHeight, string startHint)
    {
        var lines = new List<string>();

        for (var row = 0; row < bodyHeight; row++)
        {
            lines.Add(string.Empty);
        }

        var top = Math.Max(0, bodyHeight - BlockHeight) / 2;

        if (bodyHeight >= BlockHeight)
        {
            lines[top] = Center(Title, width);
            lines[top + 2] = Center(startHint, width);
        }
        else if (bodyHeight > 0)
        {
            lines[0] = Center(startHint, width);
        }

        return Normalize(lines, width, bodyHeight);
    }

    private static List<string> Normalize(List<string> lines, int width, int height)
    {
        var padded = lines.Take(height).Select(line => TextWidth.Pad(line, width)).ToList();

        padded.AddRange(Blank(width, height - lines.Count));

        return padded;
    }

    private static IEnumerable<string> Blank(int width, int count)
    {
        return Enumerable.Repeat(new string(' ', width), Math.Max(0, count));
    }

    private static string Center(string text, int width)
    {
        var fitted = TextWidth.Fit(text, width);

        return $"{new string(' ', (width - TextWidth.Of(fitted)) / 2)}{fitted}";
    }
}
