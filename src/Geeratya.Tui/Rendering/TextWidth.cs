using System.Globalization;
using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>Terminal column width of text. Wide characters take two columns and are never split.</summary>
public static class TextWidth
{
    public static int Of(string text) => DisplayWidth.Of(text);

    public static int Of(Rune rune) => DisplayWidth.Of(rune);

    /// <summary>
    /// Cuts <paramref name="text"/> to at most <paramref name="width"/> columns without splitting a character.
    /// </summary>
    public static string Fit(string text, int width)
    {
        var builder = new StringBuilder();
        var used = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var elementWidth = DisplayWidth.OfCluster(element);

            if (used + elementWidth > width)
            {
                break;
            }

            builder.Append(element);
            used += elementWidth;
        }

        return builder.ToString();
    }

    /// <summary>Cuts or pads with spaces to exactly <paramref name="width"/> columns.</summary>
    public static string Pad(string text, int width)
    {
        var fitted = Fit(text, width);

        return $"{fitted}{new string(' ', Math.Max(0, width - Of(fitted)))}";
    }
}
