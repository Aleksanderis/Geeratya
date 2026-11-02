using System.Buffers;
using System.Text;
using System.Text.Json;

namespace Geeratya.Jira.Links;

/// <summary>Builds the body of <c>POST /issueLink</c> by hand.</summary>
internal static class LinkRequestBody
{
    public static string Build(string typeId, string outwardKey, string inwardKey)
    {
        var buffer = new ArrayBufferWriter<byte>();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            WriteObject(writer, "type", "id", typeId);
            WriteObject(writer, "inwardIssue", "key", inwardKey);
            WriteObject(writer, "outwardIssue", "key", outwardKey);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteObject(Utf8JsonWriter writer, string name, string property, string value)
    {
        writer.WriteStartObject(name);
        writer.WriteString(property, value);
        writer.WriteEndObject();
    }
}
