using System.Globalization;
using Geeratya.Core.Text;

namespace Geeratya.Core.Adf;

/// <summary>
/// Maps the hex colour of an ADF <c>textColor</c> mark to the nearest of a few theme-aware text roles by hue, so the
/// terminal draws it in its own palette (readable on light and dark themes, nearest colour at 16 colours). Near-black,
/// near-white and unreadable values map to nothing: the text keeps its normal colour. No allocation.
/// </summary>
internal static class AdfTextColors
{
    private const int NibbleScale = 17;
    private const int MinValue = 90;
    private const int GreyChroma = 32;
    private const int WhiteFloor = 200;
    private const double YellowStart = 20;
    private const double GreenStart = 70;
    private const double TealStart = 165;
    private const double BlueStart = 200;
    private const double PurpleStart = 245;
    private const double RedStart = 335;
    private const double HueSector = 60;
    private const double HueFull = 360;

    /// <summary>The role for a colour such as <c>#ff5630</c> or <c>#f00</c>, or null when it keeps the normal colour.</summary>
    public static TextRole? RoleOf(string? color)
    {
        if (!TryParse(color, out var red, out var green, out var blue))
        {
            return null;
        }

        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var chroma = max - min;

        if (chroma < GreyChroma)
        {
            return max < MinValue || min >= WhiteFloor ? null : TextRole.ColorGray;
        }

        if (max < MinValue)
        {
            return null;
        }

        var hue = Hue(red, green, blue, max, chroma);

        return hue switch
        {
            < YellowStart => TextRole.ColorRed,
            < GreenStart => TextRole.ColorYellow,
            < TealStart => TextRole.ColorGreen,
            < BlueStart => TextRole.ColorTeal,
            < PurpleStart => TextRole.ColorBlue,
            < RedStart => TextRole.ColorPurple,
            _ => TextRole.ColorRed,
        };
    }

    private static double Hue(int red, int green, int blue, int max, int chroma)
    {
        double sector;

        if (max == red)
        {
            sector = ((green - blue) / (double)chroma) % 6;
        }
        else if (max == green)
        {
            sector = ((blue - red) / (double)chroma) + 2;
        }
        else
        {
            sector = ((red - green) / (double)chroma) + 4;
        }

        var hue = sector * HueSector;

        return hue < 0 ? hue + HueFull : hue;
    }

    private static bool TryParse(string? color, out int red, out int green, out int blue)
    {
        red = green = blue = 0;
        var text = color.AsSpan().Trim();

        if (text.Length > 0 && text[0] == '#')
        {
            text = text[1..];
        }

        if (text.Length == 3
            && TryHex(text[..1], out red)
            && TryHex(text.Slice(1, 1), out green)
            && TryHex(text.Slice(2, 1), out blue))
        {
            red *= NibbleScale;
            green *= NibbleScale;
            blue *= NibbleScale;

            return true;
        }

        return text.Length == 6
               && TryHex(text[..2], out red)
               && TryHex(text.Slice(2, 2), out green)
               && TryHex(text.Slice(4, 2), out blue);
    }

    private static bool TryHex(ReadOnlySpan<char> digits, out int value)
    {
        return int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }
}
