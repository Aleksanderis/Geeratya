using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>The characters the app draws itself, in the Unicode and the ASCII form (<c>docs/ux-screens.md</c>, "List layout").</summary>
public sealed class Glyphs
{
    private static readonly Glyphs _unicode = new(" · ", "…", "○", "◐", "●");
    private static readonly Glyphs _ascii = new(" - ", "...", ".", "~", "x");

    private static readonly Dictionary<char, char> _asciiOf = BuildAsciiMap();

    private Glyphs(string separator, string ellipsis, string toDo, string inProgress, string done)
    {
        Separator = separator;
        Ellipsis = ellipsis;
        ToDo = toDo;
        InProgress = inProgress;
        Done = done;
    }

    /// <summary>Between the parts of a header line.</summary>
    public string Separator { get; }

    /// <summary>Marks a loading text.</summary>
    public string Ellipsis { get; }

    /// <summary>Status category "new".</summary>
    public string ToDo { get; }

    /// <summary>Status category "indeterminate".</summary>
    public string InProgress { get; }

    /// <summary>Status category "done".</summary>
    public string Done { get; }

    /// <summary>
    /// Swaps every character the shell draws outside ASCII (box lines, arrows, marks, separators) for one ASCII character,
    /// one cell for one cell, so a line keeps its width. Other text, such as issue content, is not touched.
    /// </summary>
    public static string ToAscii(string text)
    {
        if (text.All(static character => character < 0x80 || !_asciiOf.ContainsKey(character)))
        {
            return text;
        }

        return string.Concat(text.Select(static character => _asciiOf.TryGetValue(character, out var ascii) ? ascii : character));
    }

    public static Glyphs For(GlyphSet set)
    {
        return set == GlyphSet.Ascii ? _ascii : _unicode;
    }

    private static Dictionary<char, char> BuildAsciiMap()
    {
        var map = new Dictionary<char, char>
        {
            ['‹'] = '<',
            ['›'] = '>',
            ['↑'] = '^',
            ['▾'] = 'v',
            ['…'] = '.',
            ['·'] = '-',
            ['○'] = '.',
            ['◐'] = '~',
            ['●'] = 'x',
        };

        foreach (var character in "─━") { map[character] = '-'; }
        foreach (var character in "│┃") { map[character] = '|'; }
        foreach (var character in "┌┐└┘├┤┬┴┼┏┓┗┛┣┫╭╮╰╯") { map[character] = '+'; }

        return map;
    }
}
