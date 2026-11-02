using System.Globalization;

namespace Geeratya.Core.Forms;

/// <summary>Reads the text of a number, date or date-time field; the same rules check and convert it.</summary>
public static class FormValueParser
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    private static readonly string[] _dateTimeFormats = [DateTimeFormat, "yyyy-MM-dd'T'HH:mm"];

    public static bool TryNumber(string text, out double number)
    {
        var parsed = double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out number);

        return parsed && double.IsFinite(number);
    }

    public static bool TryDate(string text, out DateOnly date)
    {
        return DateOnly.TryParseExact(
            text.Trim(),
            DateFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out date);
    }

    /// <summary>A moment typed in the local time zone.</summary>
    public static bool TryDateTime(string text, out DateTimeOffset moment)
    {
        return DateTimeOffset.TryParseExact(
            text.Trim(),
            _dateTimeFormats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeLocal,
            out moment);
    }
}
