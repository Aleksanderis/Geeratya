using System.Globalization;

namespace Geeratya.Core.Text;

/// <summary>Formats a byte count for people: binary units, one decimal from KiB up.</summary>
public static class FileSize
{
    private const double Step = 1024;
    private const int MaxUnit = 4;

    private static readonly string[] _units = ["B", "KiB", "MiB", "GiB", "TiB"];

    /// <summary>"0 B", "512 B", "1.5 KiB", "2.0 MiB"; a negative count reads as 0 B.</summary>
    public static string Format(long bytes)
    {
        if (bytes < Step)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, bytes)} {_units[0]}");
        }

        var value = (double)bytes;
        var unit = 0;

        while (unit < MaxUnit && Math.Round(value, 1, MidpointRounding.AwayFromZero) >= Step)
        {
            value /= Step;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{value:0.0} {_units[unit]}");
    }
}
