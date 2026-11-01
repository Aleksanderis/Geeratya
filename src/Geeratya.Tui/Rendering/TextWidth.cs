using System.Globalization;
using System.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>Terminal column width of text. Wide characters take two columns and are never split.</summary>
public static class TextWidth
{
    private const int WideWidth = 2;
    private const int EmojiPresentationSelector = 0xFE0F;

    private static readonly WideRange[] _wideRanges =
    [
        new(0x1100, 0x115F),
        new(0x2E80, 0xA4CF),
        new(0xAC00, 0xD7A3),
        new(0xF900, 0xFAFF),
        new(0xFE30, 0xFE6F),
        new(0xFF00, 0xFF60),
        new(0xFFE0, 0xFFE6),
        new(0x1F1E6, 0x1F1FF),
        new(0x1F300, 0x1F64F),
        new(0x1F680, 0x1F6FF),
        new(0x1F900, 0x1F9FF),
        new(0x1FA70, 0x1FAFF),
        new(0x2705, 0x2705),
        new(0x2728, 0x2728),
        new(0x274C, 0x274C),
        new(0x26A1, 0x26A1),
        new(0x20000, 0x3FFFD),
    ];

    public static int Of(string text)
    {
        var width = 0;
        var elements = StringInfo.GetTextElementEnumerator(text);

        while (elements.MoveNext())
        {
            width += OfCluster(elements.GetTextElement());
        }

        return width;
    }

    public static int Of(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);

        if (category is UnicodeCategory.Control
            or UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format)
        {
            return 0;
        }

        return IsWide(rune.Value) ? WideWidth : 1;
    }

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
            var elementWidth = OfCluster(element);

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

    private static int OfCluster(string cluster)
    {
        var width = 0;

        foreach (var rune in cluster.EnumerateRunes())
        {
            width = Math.Max(width, Of(rune));

            if (rune.Value == EmojiPresentationSelector)
            {
                width = WideWidth;
            }
        }

        return width;
    }

    private static bool IsWide(int codePoint)
    {
        foreach (var range in _wideRanges)
        {
            if (codePoint >= range.First && codePoint <= range.Last)
            {
                return true;
            }
        }

        return false;
    }

    private readonly record struct WideRange(int First, int Last);
}
