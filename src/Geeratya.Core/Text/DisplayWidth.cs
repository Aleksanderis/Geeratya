using System.Globalization;
using System.Text;

namespace Geeratya.Core.Text;

/// <summary>
/// Terminal column width of text. East Asian Wide and Fullwidth characters and emoji with default emoji
/// presentation take two columns, combining marks and controls none. The wide table is generated from the Unicode
/// Character Database by <c>scripts/gen-display-width.py</c>.
/// </summary>
public static class DisplayWidth
{
    private const int WideWidth = 2;
    private const int EmojiPresentationSelector = 0xFE0F;

    /// <summary>Width of a string, measured per grapheme cluster.</summary>
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

    /// <summary>Width of one code point: 0, 1 or 2.</summary>
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

    /// <summary>Width of one grapheme cluster: the widest code point, or 2 when it contains an emoji selector.</summary>
    public static int OfCluster(string cluster)
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

    /// <summary>Whether the code point is East Asian Wide or Fullwidth, or has emoji presentation.</summary>
    public static bool IsWide(int codePoint)
    {
        var bounds = WideRanges.Bounds;
        var low = 0;
        var high = (bounds.Length / WideWidth) - 1;

        while (low <= high)
        {
            var middle = (low + high) / WideWidth;

            if (codePoint < bounds[middle * WideWidth])
            {
                high = middle - 1;
            }
            else if (codePoint > bounds[(middle * WideWidth) + 1])
            {
                low = middle + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
