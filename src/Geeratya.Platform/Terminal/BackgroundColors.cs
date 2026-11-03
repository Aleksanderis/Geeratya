using System.Globalization;

namespace Geeratya.Platform.Terminal;

/// <summary>Reads "light or dark" out of the two ways a terminal can tell its background: an OSC 11 answer and <c>COLORFGBG</c>.</summary>
public static class BackgroundColors
{
    private const int AnsiLightestIndex = 15;
    private const int AnsiDarkGreyIndex = 8;
    private const int ChannelCount = 3;
    private const int MaxChannelDigits = 4;
    private const double LightThreshold = 0.5;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private static readonly char[] _answerEnds = ['\u0007', '\u001b', '\\'];

    /// <summary>
    /// Reads a colour from an OSC 11 answer (<c>rgb:RRRR/GGGG/BBBB</c>, 1 to 4 hex digits per channel, optionally
    /// <c>rgba:</c>) and says whether it is light (weighted luminance of the encoded channels above one half: a
    /// "light or dark" heuristic, not a colour-science value). Null when the text holds no colour.
    /// </summary>
    public static bool? ParseOsc11(string response)
    {
        var start = response.IndexOf("rgb", StringComparison.Ordinal);
        var colon = start < 0 ? -1 : response.IndexOf(':', start);

        if (colon < 0)
        {
            return null;
        }

        var body = response[(colon + 1)..];
        var end = body.IndexOfAny(_answerEnds);

        if (end >= 0)
        {
            body = body[..end];
        }

        var parts = body.Split('/');

        if (parts.Length is not (ChannelCount or ChannelCount + 1))
        {
            return null;
        }

        var luminance = 0.0;
        double[] weights = [RedWeight, GreenWeight, BlueWeight];

        for (var i = 0; i < ChannelCount; i++)
        {
            var digits = parts[i];

            if (digits.Length is < 1 or > MaxChannelDigits
                || !int.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
            {
                return null;
            }

            luminance += weights[i] * value / (Math.Pow(16, digits.Length) - 1);
        }

        return luminance > LightThreshold;
    }

    /// <summary>
    /// Reads <c>COLORFGBG</c> (<c>foreground;background</c>, sometimes with a middle field) as an ANSI colour index of
    /// the background: 7 and 9 to 15 are light, 0 to 6 and 8 are dark. Null when unset or not a number.
    /// </summary>
    public static bool? ParseColorFgBg(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var background = value[(value.LastIndexOf(';') + 1)..];

        if (!int.TryParse(background, NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index > AnsiLightestIndex)
        {
            return null;
        }

        return index is 7 or (> AnsiDarkGreyIndex and <= AnsiLightestIndex);
    }
}
