using Geeratya.Platform.Terminal;

namespace Geeratya.Tui.Rendering;

/// <summary>The characters the app draws itself, in the Unicode and the ASCII form (<c>docs/ux-screens.md</c>, "List layout").</summary>
public sealed class Glyphs
{
    private static readonly Glyphs _unicode = new(" · ", "…", "○", "◐", "●");
    private static readonly Glyphs _ascii = new(" - ", "...", ".", "~", "x");

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

    public static Glyphs For(GlyphSet set)
    {
        return set == GlyphSet.Ascii ? _ascii : _unicode;
    }
}
