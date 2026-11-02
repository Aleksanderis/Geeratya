using System.Globalization;

namespace Geeratya.Core.Detail;

/// <summary>Short relative times such as "5 min ago". Pure: the reference time is passed in.</summary>
public static class RelativeTime
{
    private const int MinutesPerHour = 60;
    private const int HoursPerDay = 24;
    private const int AbsoluteAfterDays = 30;

    /// <summary>
    /// How long before <paramref name="reference"/> the time was; dates older than 30 days and times after the
    /// reference show as dates.
    /// </summary>
    public static string Format(DateTimeOffset time, DateTimeOffset reference)
    {
        var age = reference - time;

        if (age < TimeSpan.Zero || age.TotalDays >= AbsoluteAfterDays)
        {
            return time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        if (age.TotalMinutes < 1)
        {
            return "just now";
        }

        if (age.TotalMinutes < MinutesPerHour)
        {
            return $"{(int)age.TotalMinutes} min ago";
        }

        return age.TotalHours < HoursPerDay ? $"{(int)age.TotalHours} h ago" : $"{(int)age.TotalDays} d ago";
    }
}
