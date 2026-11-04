using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>Terminal column width of text. Wide characters take two columns and are never split.</summary>
public static class TextWidth
{
    public static int Of(string text) => DisplayWidth.Of(text);

    public static int Of(Rune rune) => DisplayWidth.Of(rune);

    /// <summary>Cuts to at most <paramref name="width"/> columns without splitting a character.</summary>
    public static string Fit(string text, int width) => DisplayWidth.Fit(text, width);

    /// <summary>Cuts or pads with spaces to exactly <paramref name="width"/> columns.</summary>
    public static string Pad(string text, int width)
    {
        var fitted = Fit(text, width);

        return $"{fitted}{new string(' ', Math.Max(0, width - Of(fitted)))}";
    }

    /// <summary>Cuts or pads on the left with spaces to exactly <paramref name="width"/> columns (right-aligned).</summary>
    public static string PadLeft(string text, int width)
    {
        var fitted = Fit(text, width);

        return $"{new string(' ', Math.Max(0, width - Of(fitted)))}{fitted}";
    }
}
