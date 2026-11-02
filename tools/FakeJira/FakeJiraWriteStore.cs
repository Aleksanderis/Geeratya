using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The in-memory side of the fake's write routes: created issues, edits laid over recorded issues, transitions,
/// comments and rank calls. Replies reuse recorded fixtures wherever one exists; nothing leaves the process.
/// </summary>
public sealed class FakeJiraWriteStore
{
    private const string SelfPrefix = "https://example.atlassian.net/rest/api/3/issue/";
    private const string FieldsName = "fields";
    private const string SummaryName = "summary";
    private const string LabelsName = "labels";
    private const string DescriptionName = "description";
    private const string DueDateName = "duedate";
    private const string TransitionsName = "transitions";
    private const string TransitionName = "transition";
    private const string CommentsName = "comments";
    private const string BodyName = "body";
    private const string IdName = "id";
    private const string KeyName = "key";
    private const string NameName = "name";
    private const string ToName = "to";
    private const string StatusName = "status";
    private const string IssueTypeName = "issuetype";
    private const string ProjectName = "project";
    private const string TotalName = "total";
    private const string InitialStatusTransitionId = "11";
    private const string CreatedName = "created";
    private const string UpdatedName = "updated";
    private const int FirstCreatedNumber = 100;
    private const int FirstCreatedId = 20000;
    private const int FirstCommentId = 30000;
    private const int NoContent = 204;
    private const int Created = 201;
    private const int BadRequest = 400;
    private const int Forbidden = 403;
    private const int NotFound = 404;

    private const string NotFoundFixture = "errors/issue-404.json";
    private const string DeleteForbiddenFixture = "errors/issue-delete-403-no-permission.json";
    private const string CreateTemplateFixture = "writes/create-gtm-task-reply.json";
    private const string MissingFieldsFixture = "errors/create-400-missing-summary.json";
    private const string NoSummaryFixture = "writes/create-400-no-summary.json";
    private const string BadIssueTypeFixture = "errors/create-400-bad-issuetype.json";
    private const string UnknownFieldFixture = "adf/edit-400-unknown-field.json";
    private const string BadTransitionFixture = "transitions/transition-400-bad-id.json";
    private const string MissingRankIssueFixture = "boards/rank-400-missing-issue.json";
    private const string CommentTemplateFixture = "writes/comment-post-gtm-reply.json";
    private const string EmptyCommentsFixture = "comments/comments-empty.json";
    private const string GtmTransitionsFixture = "transitions/transitions-gtm-5.json";
    private const string GcmTransitionsFixture = "transitions/transitions-gcm-1.json";
    private const string GcmPrefix = "GCM-";

    private static readonly string[] _editableFields = [SummaryName, LabelsName, DescriptionName, DueDateName];

    private readonly Func<string, byte[]> _readFixture;
    private readonly Func<string, bool> _isRecordedIssue;
    private readonly Dictionary<string, StoredIssue> _issues = [];
    private readonly Dictionary<string, int> _nextNumber = new() { ["GCM"] = FirstCreatedNumber, ["GTM"] = FirstCreatedNumber };
    private readonly Lock _lock = new();
    private int _nextId = FirstCreatedId;
    private int _nextCommentId = FirstCommentId;

    public FakeJiraWriteStore(Func<string, byte[]> readFixture, Func<string, bool> isRecordedIssue)
    {
        _readFixture = readFixture;
        _isRecordedIssue = isRecordedIssue;
    }

    /// <summary>True when the issue was created through the fake or has edits to lay over its recorded reply.</summary>
    public bool Knows(string key)
    {
        lock (_lock)
        {
            return _issues.ContainsKey(key);
        }
    }

    /// <summary>The issue with edits applied; null when the fake holds nothing for it.</summary>
    public byte[]? Issue(string key, byte[]? recorded)
    {
        lock (_lock)
        {
            if (!_issues.TryGetValue(key, out var stored))
            {
                return null;
            }

            var node = stored.IsCreated || recorded is null ? CreatedIssueNode(stored) : JsonNode.Parse(recorded)!;

            if (!stored.IsCreated)
            {
                ApplyEdits(node, stored);
            }

            return Encoding.UTF8.GetBytes(node.ToJsonString());
        }
    }

    public FakeReply Create(string body)
    {
        var fields = FieldsOf(body);

        if (fields is null)
        {
            return Reply(BadRequest, MissingFieldsFixture);
        }

        var projectKey = StringOf((fields[ProjectName] as JsonObject)?[KeyName]);
        var typeName = StringOf((fields[IssueTypeName] as JsonObject)?[NameName]);
        var summary = StringOf(fields[SummaryName]);

        lock (_lock)
        {
            if (projectKey is null || !_nextNumber.ContainsKey(projectKey) || typeName is null)
            {
                return Reply(BadRequest, BadIssueTypeFixture);
            }

            if (string.IsNullOrWhiteSpace(summary))
            {
                return Reply(BadRequest, NoSummaryFixture);
            }

            var number = _nextNumber[projectKey]++;
            var key = $"{projectKey}-{number.ToString(CultureInfo.InvariantCulture)}";
            var stored = new StoredIssue(key, (_nextId++).ToString(CultureInfo.InvariantCulture), true)
            {
                IssueType = typeName,
            };
            stored.Fields[SummaryName] = JsonValue.Create(summary);
            stored.Fields[LabelsName] = fields[LabelsName]?.DeepClone() ?? new JsonArray();
            _issues[key] = stored;

            var reply = JsonNode.Parse(_readFixture(CreateTemplateFixture))!;
            reply[IdName] = stored.Id;
            reply[KeyName] = key;
            reply["self"] = $"{SelfPrefix}{stored.Id}";

            return new FakeReply(Created, Encoding.UTF8.GetBytes(reply.ToJsonString()));
        }
    }

    public FakeReply Edit(string key, string body)
    {
        var fields = FieldsOf(body);

        if (fields is null)
        {
            // An "update" edit is a real Jira shape the fake does not model; say so instead of "summary required".
            return Reply(BadRequest, body.Contains("\"update\"", StringComparison.Ordinal) ? UnknownFieldFixture : MissingFieldsFixture);
        }

        foreach (var field in fields)
        {
            if (!_editableFields.Contains(field.Key))
            {
                return Reply(BadRequest, UnknownFieldFixture);
            }
        }

        if (fields.ContainsKey(SummaryName) && string.IsNullOrWhiteSpace(StringOf(fields[SummaryName])))
        {
            return Reply(BadRequest, NoSummaryFixture);
        }

        lock (_lock)
        {
            if (Find(key) is not { } stored)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            foreach (var field in fields)
            {
                stored.Fields[field.Key] = field.Value?.DeepClone();
            }

            return Empty(NoContent);
        }
    }

    public FakeReply Delete(string key)
    {
        lock (_lock)
        {
            if (_issues.TryGetValue(key, out var stored) && stored.IsCreated)
            {
                _issues.Remove(key);

                return Empty(NoContent);
            }

            // A recorded issue is not ours to delete: the dev site's company-managed project refuses it too.
            return _isRecordedIssue(key) ? Reply(Forbidden, DeleteForbiddenFixture) : Reply(NotFound, NotFoundFixture);
        }
    }

    public FakeReply Transitions(string key)
    {
        lock (_lock)
        {
            return Find(key) is null ? Reply(NotFound, NotFoundFixture) : Reply(200, TransitionsFixtureOf(key));
        }
    }

    public FakeReply Transition(string key, string body)
    {
        var id = ReadTransitionId(body);

        lock (_lock)
        {
            if (Find(key) is not { } stored)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            var target = id is null ? null : StatusAfter(key, id);

            if (target is null)
            {
                return Reply(BadRequest, BadTransitionFixture);
            }

            stored.Status = target;

            return Empty(NoContent);
        }
    }

    public FakeReply AddComment(string key, string body)
    {
        var comment = ReadCommentBody(body);

        lock (_lock)
        {
            if (Find(key) is not { } stored)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            if (comment is null)
            {
                return Reply(BadRequest, UnknownFieldFixture);
            }

            var entry = (JsonObject)JsonNode.Parse(_readFixture(CommentTemplateFixture))!;
            var id = (_nextCommentId++).ToString(CultureInfo.InvariantCulture);
            entry[IdName] = id;
            entry[BodyName] = comment;
            entry["self"] = $"{SelfPrefix}{stored.Id}/comment/{id}";
            stored.Comments.Add(entry);

            return new FakeReply(Created, Encoding.UTF8.GetBytes(entry.ToJsonString()));
        }
    }

    public FakeReply EditComment(string key, string commentId, string body)
    {
        var comment = ReadCommentBody(body);

        lock (_lock)
        {
            var entry = FindComment(key, commentId);

            if (entry is null)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            if (comment is null)
            {
                return Reply(BadRequest, UnknownFieldFixture);
            }

            entry[BodyName] = comment;
            entry[UpdatedName] = entry[CreatedName]?.DeepClone();

            return new FakeReply(200, Encoding.UTF8.GetBytes(entry.ToJsonString()));
        }
    }

    public FakeReply DeleteComment(string key, string commentId)
    {
        lock (_lock)
        {
            if (Find(key) is not { } stored)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            if (FindComment(key, commentId) is not { } entry)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            stored.Comments.Remove(entry);

            return Empty(NoContent);
        }
    }

    /// <summary>The comments list: the recorded one (or an empty one) plus the comments added through the fake.</summary>
    public byte[]? Comments(string key, byte[]? recorded)
    {
        lock (_lock)
        {
            if (!_issues.TryGetValue(key, out var stored))
            {
                return null;
            }

            var node = (JsonObject)JsonNode.Parse(recorded ?? _readFixture(EmptyCommentsFixture))!;
            var list = node[CommentsName] as JsonArray ?? [];

            foreach (var entry in stored.Comments)
            {
                list.Add(entry.DeepClone());
            }

            node[CommentsName] = list;
            node[TotalName] = list.Count;

            return Encoding.UTF8.GetBytes(node.ToJsonString());
        }
    }

    public FakeReply Rank(string body)
    {
        var keys = ReadRankKeys(body);

        lock (_lock)
        {
            if (keys.Count == 0 || keys.Any(key => Find(key) is null))
            {
                return Reply(BadRequest, MissingRankIssueFixture);
            }

            return Empty(NoContent);
        }
    }

    private static List<string> ReadRankKeys(string body)
    {
        var keys = new List<string>();

        try
        {
            var node = JsonNode.Parse(body) as JsonObject;

            foreach (var item in node?["issues"] as JsonArray ?? [])
            {
                keys.Add(item?.GetValue<string>() ?? string.Empty);
            }

            foreach (var name in new[] { "rankBeforeIssue", "rankAfterIssue" })
            {
                if (node?[name]?.GetValue<string>() is { } reference)
                {
                    keys.Add(reference);
                }
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or FormatException)
        {
            return [];
        }

        return keys;
    }

    private static JsonObject? FieldsOf(string body)
    {
        try
        {
            return (JsonNode.Parse(body) as JsonObject)?[FieldsName] as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadTransitionId(string body)
    {
        try
        {
            return (JsonNode.Parse(body) as JsonObject)?[TransitionName]?[IdName]?.GetValue<string>();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return null;
        }
    }

    private static JsonNode? ReadCommentBody(string body)
    {
        try
        {
            return (JsonNode.Parse(body) as JsonObject)?[BodyName]?.DeepClone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void ApplyEdits(JsonNode node, StoredIssue stored)
    {
        if (node[FieldsName] is not JsonObject fields)
        {
            return;
        }

        foreach (var field in stored.Fields)
        {
            fields[field.Key] = field.Value?.DeepClone();
        }

        if (stored.Status is not null)
        {
            fields[StatusName] = stored.Status.DeepClone();
        }
    }

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static FakeReply Empty(int status) => new(status, []);

    private FakeReply Reply(int status, string fixture) => new(status, _readFixture(fixture));

    private static string TransitionsFixtureOf(string key) =>
        key.StartsWith(GcmPrefix, StringComparison.Ordinal) ? GcmTransitionsFixture : GtmTransitionsFixture;

    private JsonNode? StatusAfter(string key, string transitionId)
    {
        var list = JsonNode.Parse(_readFixture(TransitionsFixtureOf(key)))?[TransitionsName] as JsonArray;

        return list?.FirstOrDefault(entry => entry?[IdName]?.GetValue<string>() == transitionId)?[ToName]?.DeepClone();
    }

    /// <summary>The stored issue; a recorded one gets an edit layer on first use. Null for an unknown key.</summary>
    private StoredIssue? Find(string key)
    {
        if (_issues.TryGetValue(key, out var stored))
        {
            return stored;
        }

        if (!_isRecordedIssue(key))
        {
            return null;
        }

        stored = new StoredIssue(key, key, false);
        _issues[key] = stored;

        return stored;
    }

    private JsonObject? FindComment(string key, string commentId)
    {
        return Find(key)?.Comments.FirstOrDefault(entry => entry[IdName]?.GetValue<string>() == commentId);
    }

    private JsonObject CreatedIssueNode(StoredIssue stored)
    {
        var status = stored.Status ?? StatusAfter(stored.Key, InitialStatusTransitionId);
        var projectKey = stored.Key[..stored.Key.IndexOf('-')];
        var fields = new JsonObject
        {
            [IssueTypeName] = new JsonObject { [NameName] = stored.IssueType },
            [ProjectName] = new JsonObject { [KeyName] = projectKey },
            [StatusName] = status?.DeepClone(),
        };

        foreach (var field in stored.Fields)
        {
            fields[field.Key] = field.Value?.DeepClone();
        }

        return new JsonObject
        {
            [IdName] = stored.Id,
            [KeyName] = stored.Key,
            ["self"] = $"{SelfPrefix}{stored.Id}",
            [FieldsName] = fields,
        };
    }
}
