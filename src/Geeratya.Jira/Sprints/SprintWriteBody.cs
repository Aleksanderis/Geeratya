using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Geeratya.Jira.Sprints;

/// <summary>Builds the bodies of the sprint writes by hand (no reflection, AOT safe).</summary>
internal static class SprintWriteBody
{
    public const string FutureState = "future";
    public const string ActiveState = "active";
    public const string ClosedState = "closed";

    private const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    public static string Create(
        string name,
        long originBoardId,
        string? goal,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("name", name);
            writer.WriteNumber("originBoardId", originBoardId);
            WriteOptional(writer, "goal", goal);
            WriteDate(writer, "startDate", startDate);
            WriteDate(writer, "endDate", endDate);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>A partial update: only the members present are sent, so nothing else changes.</summary>
    public static string Update(string? name, string? goal, DateTimeOffset? startDate, DateTimeOffset? endDate) =>
        Partial(null, name, goal, startDate, endDate);

    public static string Start(DateTimeOffset startDate, DateTimeOffset endDate) =>
        Partial(ActiveState, null, null, startDate, endDate);

    public static string Complete() => Partial(ClosedState, null, null, null, null);

    private static string Partial(
        string? state,
        string? name,
        string? goal,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteOptional(writer, "state", state);
            WriteOptional(writer, "name", name);
            WriteOptional(writer, "goal", goal);
            WriteDate(writer, "startDate", startDate);
            WriteDate(writer, "endDate", endDate);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteOptional(Utf8JsonWriter writer, string property, string? value)
    {
        if (value is not null)
        {
            writer.WriteString(property, value);
        }
    }

    private static void WriteDate(Utf8JsonWriter writer, string property, DateTimeOffset? value)
    {
        if (value is { } date)
        {
            writer.WriteString(property, date.UtcDateTime.ToString(DateFormat, CultureInfo.InvariantCulture));
        }
    }
}
