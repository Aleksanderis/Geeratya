using System.Text;

namespace Geeratya.Core.Text;

/// <summary>Cuts text to a number of terminal cells.</summary>
public static class DisplayText
{
    /// <summary>The text cut to <paramref name="width"/> cells, ending in an ellipsis when something was cut.</summary>
    public static string Cut(string text, int width)
    {
        if (width <= 0)
        {
            return string.Empty;
        }

        if (DisplayWidth.Of(text) <= width)
        {
            return text;
        }

        var builder = new StringBuilder();
        var used = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            var size = DisplayWidth.Of(rune);

            if (used + size > width - 1)
            {
                break;
            }

            builder.Append(rune.ToString());
            used += size;
        }

        return builder.Append('…').ToString();
    }
}
