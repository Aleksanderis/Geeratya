using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Geeratya.FakeJira;

/// <summary>
/// With <c>--issues n</c>: a read-only Scrum board (id 7, project <c>GEN</c>) over the generated issues. The first
/// four fifths of the issues sit in consecutive blocks of 3 to 30 sprints (the last one future, the one before it
/// active, the rest closed), the last fifth is in no sprint, and an unfinished issue of a closed sprint is also in
/// the backlog, as on the real site. Everything is computed from the issue number, so nothing is stored.
/// </summary>
public sealed partial class GeneratedScrum
{
    public const int BoardId = 7;
    public const int FirstSprintId = 5001;

    private const int MinSprints = 3;
    private const int MaxSprints = 30;
    private const int IssuesPerSprint = 100;
    private const int SprintPageCap = 50;
    private const int BoardPage = 100;
    private const string Prefix = "GEN-";
    private const string SprintField = "customfield_10020";
    private const string Values = "values";
    private const string StartAt = "startAt";
    private const string MaxResults = "maxResults";
    private const string Closed = "closed";
    private const string Active = "active";
    private const string Self = "https://example.invalid/rest/agile/1.0/";

    private static readonly string[] _statuses = ["Backlog", "To Do", "In Progress", "Done"];
    private static readonly string[] _kinds = ["Task", "Bug", "Story"];
    private static readonly DateTime _firstStart = new(2026, 1, 5, 9, 0, 0, DateTimeKind.Utc);

    private readonly int _total;
    private readonly int _sprintCount;
    private readonly int _inSprints;
    private readonly int _block;
    private readonly Func<string, byte[]> _readFixture;

    public GeneratedScrum(int total, Func<string, byte[]> readFixture)
    {
        _total = total;
        _readFixture = readFixture;
        _sprintCount = Math.Clamp(total / IssuesPerSprint, MinSprints, MaxSprints);
        _inSprints = total - (total / 5);
        _block = Math.Max(1, (_inSprints + _sprintCount - 1) / _sprintCount);
    }

    [GeneratedRegex(@"sprint\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SprintEqualsJql();

    /// <summary>The recorded company-managed board entry rewritten for the generated board.</summary>
    public JsonObject BoardEntry()
    {
        var board = Parse("scrum/board-gcs.json");
        board["id"] = BoardId;
        board["name"] = "GEN board";
        board["self"] = $"{Self}board/{BoardId}";
        board["location"]!["projectKey"] = "GEN";
        board["location"]!["name"] = "GEN (GEN)";
        board["location"]!["displayName"] = "GEN (GEN)";
        board["location"]!["projectName"] = "GEN";

        return board;
    }

    /// <summary>The reply for a request below <c>board/7</c> or on a generated sprint; null otherwise.</summary>
    public FakeReply? Route(string[] parts, IReadOnlyDictionary<string, string> query)
    {
        if (parts[0] == "board" && parts.Length >= 2 && parts[1] == BoardId.ToString(CultureInfo.InvariantCulture))
        {
            return (parts.Length, parts.Length > 2 ? parts[2] : string.Empty) switch
            {
                (2, _) => Json(BoardEntry()),
                (3, "configuration") => Json(Configuration()),
                (3, "epic") => Json(Empty()),
                (3, "sprint") => SprintList(query),
                (3, "issue") => Page(BoardNumbers(query), query, BoardPage),
                (3, "backlog") => Page(BacklogNumbers(), query, BoardPage),
                _ => null,
            };
        }

        if (parts[0] == "sprint" && parts.Length >= 2 && int.TryParse(parts[1], out var id) && IsGenerated(id))
        {
            return parts.Length switch
            {
                2 => Json(SprintEntry(id)),
                3 when parts[2] == "issue" => Page(Members(id), query, SprintPageCap),
                _ => null,
            };
        }

        return null;
    }

    private static bool IsFinished(int number) => number % _statuses.Length == 3;

    private static FakeReply Json(JsonNode node)
    {
        return new FakeReply(200, Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    private static JsonObject Empty()
    {
        return new JsonObject { [MaxResults] = 50, [StartAt] = 0, ["isLast"] = true, [Values] = new JsonArray() };
    }

    private static int IntOf(IReadOnlyDictionary<string, string> query, string name, int fallback)
    {
        var value = fallback;
        _ = query.TryGetValue(name, out var text)
            && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        return value;
    }

    private static string DateOf(int sprintIndex, int days)
    {
        var date = _firstStart.AddDays((sprintIndex * 14) + days);

        return date.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    private JsonObject Parse(string fixture) => JsonNode.Parse(_readFixture(fixture))!.AsObject();

    private bool IsGenerated(int id) => id >= FirstSprintId && id < FirstSprintId + _sprintCount;

    private JsonObject Configuration()
    {
        var configuration = Parse("scrum/board-gcs-config.json");
        configuration["id"] = BoardId;
        configuration["name"] = "GEN board";
        configuration["self"] = $"{Self}board/{BoardId}/configuration";

        return configuration;
    }

    private string StateOfSprint(int index)
    {
        if (index == _sprintCount - 1)
        {
            return "future";
        }

        return index == _sprintCount - 2 ? Active : Closed;
    }

    private JsonObject SprintEntry(int id)
    {
        var index = id - FirstSprintId;
        var state = StateOfSprint(index);
        var sprint = new JsonObject
        {
            ["id"] = id,
            ["self"] = $"{Self}sprint/{id}",
            ["state"] = state,
            ["name"] = $"Generated Sprint {index + 1}",
            ["startDate"] = DateOf(index, 0),
            ["endDate"] = DateOf(index, 13),
            ["originBoardId"] = BoardId,
            ["goal"] = $"Generated goal {index + 1}",
        };

        if (state == Closed)
        {
            sprint["completeDate"] = DateOf(index, 13);
        }

        return sprint;
    }

    private FakeReply SprintList(IReadOnlyDictionary<string, string> query)
    {
        var states = query.TryGetValue("state", out var text) ? text.Split(',') : [];

        if (states.Any(state => state is not (Active or Closed or "future")))
        {
            return new FakeReply(400, _readFixture("scrum/error-sprints-state-invalid-400.json"));
        }

        var matching = Enumerable.Range(0, _sprintCount)
            .Where(index => states.Length == 0 || states.Contains(StateOfSprint(index)))
            .ToList();
        var start = Math.Max(IntOf(query, StartAt, 0), 0);
        var size = Math.Clamp(IntOf(query, MaxResults, SprintPageCap), 1, SprintPageCap);
        var values = new JsonArray();

        foreach (var index in matching.Skip(start).Take(size))
        {
            values.Add((JsonNode)SprintEntry(FirstSprintId + index));
        }

        var page = new JsonObject
        {
            ["isLast"] = start + size >= matching.Count,
            [MaxResults] = size,
            [StartAt] = start,
            ["total"] = matching.Count,
            [Values] = values,
        };

        return Json(page);
    }

    /// <summary>The sprint index (0-based) an issue number sits in, or -1 for none.</summary>
    private int SprintIndexOf(int number)
    {
        return number > _inSprints ? -1 : (number - 1) / _block;
    }

    private List<int> Members(int sprintId)
    {
        var index = sprintId - FirstSprintId;

        return [.. Enumerable.Range(1, _total).Where(number => SprintIndexOf(number) == index)];
    }

    private List<int> BacklogNumbers()
    {
        return [.. Enumerable.Range(1, _total).Where(IsBacklog)];
    }

    private bool InState(int number, string state)
    {
        var index = SprintIndexOf(number);

        return index >= 0 && StateOfSprint(index) == state;
    }

    private bool IsBacklog(int number)
    {
        var index = SprintIndexOf(number);
        var open = index >= 0 && StateOfSprint(index) != Closed;

        return !open && !IsFinished(number);
    }

    private List<int> BoardNumbers(IReadOnlyDictionary<string, string> query)
    {
        var all = Enumerable.Range(1, _total);

        if (!query.TryGetValue("jql", out var jql))
        {
            return [.. all];
        }

        if (jql.Contains("futureSprints()", StringComparison.OrdinalIgnoreCase))
        {
            // The Scrum backlog source: open and future sprints plus the issues in no sprint.
            return [.. all.Where(number => SprintIndexOf(number) < 0 || StateOfSprint(SprintIndexOf(number)) != Closed)];
        }

        if (jql.Contains("openSprints()", StringComparison.OrdinalIgnoreCase))
        {
            return [.. all.Where(number => InState(number, Active))];
        }

        if (SprintEqualsJql().Match(jql) is { Success: true } match)
        {
            var id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            return [.. all.Where(number => SprintIndexOf(number) == id - FirstSprintId)];
        }

        return [.. all];
    }

    private FakeReply Page(List<int> numbers, IReadOnlyDictionary<string, string> query, int defaultSize)
    {
        var start = Math.Max(IntOf(query, StartAt, 0), 0);
        var size = Math.Max(IntOf(query, MaxResults, defaultSize), 1);
        var buffer = new MemoryStream();

        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("expand", "schema,names");
            writer.WriteNumber(StartAt, start);
            writer.WriteNumber(MaxResults, size);
            writer.WriteNumber("total", numbers.Count);
            writer.WriteStartArray("issues");

            foreach (var number in numbers.Skip(start).Take(size))
            {
                WriteIssue(writer, number);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return new FakeReply(200, buffer.ToArray());
    }

    private void WriteIssue(Utf8JsonWriter writer, int number)
    {
        var status = _statuses[number % _statuses.Length];
        var index = SprintIndexOf(number);

        writer.WriteStartObject();
        writer.WriteString("id", (10000 + number).ToString(CultureInfo.InvariantCulture));
        writer.WriteString("key", $"{Prefix}{number}");
        writer.WriteStartObject("fields");
        writer.WriteString("summary", $"Generated issue {number}");
        writer.WriteStartObject("issuetype");
        writer.WriteString("name", _kinds[number % _kinds.Length]);
        writer.WriteBoolean("subtask", false);
        writer.WriteEndObject();
        writer.WriteStartObject("status");
        writer.WriteString("name", status);
        writer.WriteStartObject("statusCategory");
        writer.WriteString("key", GeneratedIssues.CategoryOf(status));
        writer.WriteEndObject();
        writer.WriteEndObject();
        writer.WriteNull("assignee");

        if (index < 0)
        {
            writer.WriteNull(SprintField);
        }
        else
        {
            var sprint = SprintEntry(FirstSprintId + index);
            writer.WriteStartArray(SprintField);
            writer.WriteStartObject();
            writer.WriteNumber("id", sprint["id"]!.GetValue<int>());
            writer.WriteString("name", sprint["name"]!.GetValue<string>());
            writer.WriteString("state", sprint["state"]!.GetValue<string>());
            writer.WriteNumber("boardId", BoardId);
            writer.WriteEndObject();
            writer.WriteEndArray();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }
}
