using System.Globalization;

namespace Geeratya.AnsiToSvg;

/// <summary>Parses the numbers of the command line.</summary>
public static class CommandLine
{
    /// <summary>The positive number in the text, or 0 when it is missing or not a positive number.</summary>
    public static int ParsePositive(string? text)
    {
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0;
    }
}
