using System.Text;
using System.Text.Json;

namespace Geeratya.FakeJira;

/// <summary>Builds search pages over a generated dataset (same shape as the recorded search replies).</summary>
public static class GeneratedIssues
{
    private const int LinkedIssueNumber = 2;

    private static readonly string[] _statuses = ["Backlog", "To Do", "In Progress", "Done"];
    private const int LongThreadEvery = 5;
    private const int LongThreadComments = 120;
    private const int ShortThreadComments = 3;

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

    /// <summary>The detail of generated issue <paramref name="number"/> (description in ADF, no relations).</summary>
    public static byte[] Detail(int number)
    {
        var kind = _kinds[number % _kinds.Length];
        var status = _statuses[number % _statuses.Length];
        var json = $$$"""
            {"id":"{{{10000 + number}}}","key":"GEN-{{{number}}}","fields":{
            "summary":"Generated issue {{{number}}}",
            "status":{"name":"{{{status}}}","statusCategory":{"key":"{{{CategoryOf(status)}}}"}},
            "issuetype":{"name":"{{{kind}}}","subtask":false},
            "priority":{"name":"Medium"},
            "assignee":null,
            "reporter":{"accountId":"gen-reporter","displayName":"Generated Reporter"},
            "labels":["generated","sample"],
            "created":"2026-10-20T09:00:00.000+0000","updated":"2026-10-30T10:30:00.000+0000",
            "project":{"key":"GEN"},
            "description":{"version":1,"type":"doc","content":[
              {"type":"heading","attrs":{"level":2},"content":[{"type":"text","text":"Summary"}]},
              {"type":"paragraph","content":[{"type":"text","text":"This is generated issue {{{number}}}. It has "},{"type":"text","text":"formatted","marks":[{"type":"strong"}]},{"type":"text","text":" text, a list and a code block, so the detail screen has something to scroll over."}]},
              {"type":"bulletList","content":[
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"First generated step"}]}]},
                {"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Second generated step"}]}]}]},
              {"type":"codeBlock","attrs":{"language":"text"},"content":[{"type":"text","text":"generated --issue {{{number}}}"}]}]},
            "subtasks":[],"issuelinks":[],"attachment":[]}}
            """;

        return Encoding.UTF8.GetBytes(json.ReplaceLineEndings(string.Empty));
    }

    /// <summary>
    /// A page of the comments of generated issue <paramref name="number"/>: every fifth issue has 120 comments (to
    /// page through), the others three.
    /// </summary>
    public static byte[] Comments(int number, int startAt, int pageSize)
    {
        var total = number % LongThreadEvery == 0 ? LongThreadComments : ShortThreadComments;
        var end = Math.Min(total, Math.Max(0, startAt) + Math.Max(1, pageSize));
        var comments = new List<string>();

        for (var index = Math.Max(0, startAt); index < end; index++)
        {
            comments.Add(
                $$$"""{"id":"{{{index + 1}}}","author":{"accountId":"gen-{{{index % 3}}}","displayName":"Generated Writer {{{index % 3}}}"},"body":{"version":1,"type":"doc","content":[{"type":"paragraph","content":[{"type":"text","text":"Generated comment {{{index + 1}}} on issue {{{number}}}."}]}]},"created":"2026-10-3{{{index % 2}}}T0{{{index % 10}}}:00:00.000+0000","updated":"2026-10-3{{{index % 2}}}T0{{{index % 10}}}:00:00.000+0000"}""");
        }

        var json = $$$"""{"startAt":{{{Math.Max(0, startAt)}}},"maxResults":{{{pageSize}}},"total":{{{total}}},"comments":[{{{string.Join(',', comments)}}}]}""";

        return Encoding.UTF8.GetBytes(json);
    }

    /// <summary>
    /// The remote links of generated issue <paramref name="number"/>: issue <see cref="LinkedIssueNumber"/> has one
    /// https link (to try the browser launcher), the others none.
    /// </summary>
    public static byte[] RemoteLinks(int number)
    {
        return number == LinkedIssueNumber
            ? """
                [{"id":1,"self":"https://example.invalid/rest/api/3/issue/GEN-2/remotelink/1",
                "object":{"url":"https://example.invalid/design","title":"Generated design notes"}}]
                """u8.ToArray()
            : "[]"u8.ToArray();
    }

    public static byte[] Count(int total) => Encoding.UTF8.GetBytes($"{{\"count\":{total}}}");

    internal static string CategoryOf(string status)
    {
        return status switch
        {
            "Done" => "done",
            "In Progress" => "indeterminate",
            _ => "new",
        };
    }

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
