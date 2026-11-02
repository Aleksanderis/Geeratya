using System.Text;
using System.Text.Json;

namespace Geeratya.Jira.Transitions;

/// <summary>Builds the body of <c>POST /issue/{key}/transitions</c> by hand (no reflection, AOT safe).</summary>
internal static class TransitionRequestBody
{
    public static string Build(string transitionId, IReadOnlyDictionary<string, JsonElement>? fields, string? comment)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("transition");
            writer.WriteString("id", transitionId);
            writer.WriteEndObject();

            if (fields is { Count: > 0 })
            {
                WriteFields(writer, fields);
            }

            if (!string.IsNullOrWhiteSpace(comment))
            {
                WriteComment(writer, comment);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteFields(Utf8JsonWriter writer, IReadOnlyDictionary<string, JsonElement> fields)
    {
        writer.WriteStartObject("fields");

        foreach (var (fieldId, value) in fields)
        {
            writer.WritePropertyName(fieldId);
            value.WriteTo(writer);
        }

        writer.WriteEndObject();
    }

    // The comment rides along as update.comment[].add.body, a one-paragraph ADF document (API v3).
    private static void WriteComment(Utf8JsonWriter writer, string comment)
    {
        writer.WriteStartObject("update");
        writer.WriteStartArray("comment");
        writer.WriteStartObject();
        writer.WriteStartObject("add");
        writer.WriteStartObject("body");
        writer.WriteNumber("version", 1);
        writer.WriteString("type", "doc");
        writer.WriteStartArray("content");
        writer.WriteStartObject();
        writer.WriteString("type", "paragraph");
        writer.WriteStartArray("content");
        writer.WriteStartObject();
        writer.WriteString("type", "text");
        writer.WriteString("text", comment);
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
