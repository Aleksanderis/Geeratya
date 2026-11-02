using System.Text;
using System.Text.Json;

namespace Geeratya.Jira.Rank;

/// <summary>Builds the bodies of the rank and backlog calls by hand (no reflection, AOT safe).</summary>
internal static class RankRequestBody
{
    public static string Rank(
        IReadOnlyList<string> keys,
        string referenceName,
        string referenceKey,
        long? rankFieldId)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteIssues(writer, keys);
            writer.WriteString(referenceName, referenceKey);

            if (rankFieldId is { } id)
            {
                writer.WriteNumber("rankCustomFieldId", id);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string Backlog(IReadOnlyList<string> keys)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            WriteIssues(writer, keys);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteIssues(Utf8JsonWriter writer, IReadOnlyList<string> keys)
    {
        writer.WriteStartArray("issues");

        foreach (var key in keys)
        {
            writer.WriteStringValue(key);
        }

        writer.WriteEndArray();
    }
}
