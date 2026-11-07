using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>The characters the app draws itself, in the Unicode and the ASCII form (<c>docs/internal/ux-screens.md</c>, "List layout").</summary>
public sealed class Glyphs
{
    private static readonly Glyphs _unicode = new(" · ", "…", "○", "◐", "●", "▲", "=", "▼", "●", "▾");
    private static readonly Glyphs _ascii = new(" - ", "...", ".", "~", "x", "^", "=", "v", "*", "v");

    private static readonly Dictionary<char, char> _asciiOf = BuildAsciiMap();

    private Glyphs(string separator, string ellipsis, string toDo, string inProgress, string done, string high, string medium, string low, string dot, string dropdown)
    {
        Separator = separator;
        Ellipsis = ellipsis;
        ToDo = toDo;
        InProgress = inProgress;
        Done = done;
        PriorityHigh = high;
        PriorityMedium = medium;
        PriorityLow = low;
        StatusDot = dot;
        Dropdown = dropdown;
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

    /// <summary>Priority high and above (<c>▲</c>, ASCII <c>^</c>), drawn in the priority colour of the tier.</summary>
    public string PriorityHigh { get; }

    /// <summary>Priority medium (<c>=</c>).</summary>
    public string PriorityMedium { get; }

    /// <summary>Priority low and below (<c>▼</c>, ASCII <c>v</c>).</summary>
    public string PriorityLow { get; }

    /// <summary>The dot before a status in the Details pane (<c>●</c>, ASCII <c>*</c>).</summary>
    public string StatusDot { get; }

    /// <summary>Marks a field that opens a popup (<c>▾</c>, ASCII <c>v</c>).</summary>
    public string Dropdown { get; }

    /// <summary>
    /// Swaps every character the shell draws outside ASCII (box lines, arrows, marks, separators) for one ASCII character,
    /// one cell for one cell, so a line keeps its width. Other characters are kept, but these same characters inside issue
    /// text are swapped too (the pass cannot tell them apart, and a terminal that needs the ASCII set cannot show them anyway).
    /// A cut text ends with <c>.</c> instead of <c>…</c> to stay one cell wide.
    /// </summary>
    public static string ToAscii(string text)
    {
        if (text.All(static character => character < 0x80 || !_asciiOf.ContainsKey(character)))
        {
            return text;
        }

        return string.Concat(text.Select(static character => _asciiOf.TryGetValue(character, out var ascii) ? ascii : character));
    }

    /// <summary>Every character <see cref="ToAscii"/> swaps.</summary>
    public static IEnumerable<char> AsciiSwapped => _asciiOf.Keys;

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
            ['▸'] = '>',
            ['↑'] = '^',
            ['▲'] = '^',
            ['▼'] = 'v',
            ['▾'] = 'v',
            ['…'] = '.',
            ['→'] = '>',
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
