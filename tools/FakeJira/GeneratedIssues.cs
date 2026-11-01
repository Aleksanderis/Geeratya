using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira;

/// <summary>Builds search pages over a generated dataset (same shape as the recorded search replies).</summary>
public static class GeneratedIssues
{
    private static readonly string[] _statuses = ["Backlog", "To Do", "In Progress", "Done"];
    private static readonly string[] _kinds = ["Task", "Bug", "Story"];

    /// <summary>One page starting at <paramref name="start"/>; the next-page token is the next start index.</summary>
    public static byte[] Page(int total, int start, int pageSize)
    {
        var end = Math.Min(total, start + pageSize);
        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteBoolean("isLast", end >= total);
            writer.WriteStartArray("issues");

            for (var index = start; index < end; index++)
            {
                WriteIssue(writer, index + 1);
            }

            writer.WriteEndArray();

            if (end < total)
            {
                writer.WriteString("nextPageToken", end.ToString());
            }

            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    public static byte[] Count(int total) => Encoding.UTF8.GetBytes($"{{\"count\":{total}}}");

    private static void WriteIssue(Utf8JsonWriter writer, int number)
    {
        writer.WriteStartObject();
        writer.WriteString("id", (10000 + number).ToString());
        writer.WriteString("key", $"GEN-{number}");
        writer.WriteStartObject("fields");
        writer.WriteString("summary", $"Generated issue {number}");
        writer.WriteStartObject("issuetype");
        writer.WriteString("name", _kinds[number % _kinds.Length]);
        writer.WriteBoolean("subtask", false);
        writer.WriteEndObject();
        writer.WriteStartObject("status");
        writer.WriteString("name", _statuses[number % _statuses.Length]);
        writer.WriteEndObject();
        writer.WriteNull("assignee");
        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
