using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The in-memory side of the fake's write routes: created issues, edits laid over recorded issues, transitions,
/// comments and rank calls. Replies reuse recorded fixtures wherever one exists; nothing leaves the process.
/// </summary>
public sealed partial class FakeJiraWriteStore
{
    private const string SelfPrefix = "https://example.atlassian.net/rest/api/3/issue/";
    private const string FieldsName = "fields";
    private const string SummaryName = "summary";
    private const string LabelsName = "labels";
    private const string UpdateName = "update";
    private const string AddName = "add";
    private const string RemoveName = "remove";
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
    private const string AssigneeName = "assignee";
    private const string AccountIdName = "accountId";
    private const string DisplayNameName = "displayName";
    private const string AutomaticAccountId = "-1";
    private const string MyselfFixture = "myself/myself.json";
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

    private const string ParentName = "parent";
    private const string InvalidParentFixture = "create/synthetic-create-400-invalid-parent.json";
    private const string MissingParentFixture = "create/synthetic-create-400-missing-parent.json";
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

    private const string PriorityName = "priority";

    private static readonly string[] _editableFields =
        [SummaryName, LabelsName, DescriptionName, DueDateName, PriorityName];

    // The priorities of both recorded edit metadata, by id.
    private static readonly Dictionary<string, string> _priorityNames = new()
    {
        ["1"] = "Highest",
        ["2"] = "High",
        ["3"] = "Medium",
        ["4"] = "Low",
        ["5"] = "Lowest",
    };

    private readonly Func<string, byte[]> _readFixture;
    private readonly Func<string, bool> _isRecordedIssue;
    private readonly Func<string, bool> _isListedIssue;
    private readonly Dictionary<string, StoredIssue> _issues = [];
    private readonly Dictionary<string, int> _nextNumber = new() { ["GCM"] = FirstCreatedNumber, ["GTM"] = FirstCreatedNumber };
    private readonly Lock _lock = new();
    private readonly FakeRankOrder _order = new();
    private int _nextId = FirstCreatedId;
    private int _nextCommentId = FirstCommentId;

    public FakeJiraWriteStore(
        Func<string, byte[]> readFixture,
        Func<string, bool> isRecordedIssue,
        Func<string, bool> isListedIssue)
    {
        _readFixture = readFixture;
        _isRecordedIssue = isRecordedIssue;
        _isListedIssue = isListedIssue;
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
                return WithoutRemovedLinks(recorded);
            }

            var node = stored.IsCreated || recorded is null ? CreatedIssueNode(stored) : JsonNode.Parse(recorded)!;

            if (!stored.IsCreated)
            {
                ApplyEdits(node, stored);
            }

            ApplyLinks(node, stored);

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
        var typeObject = fields[IssueTypeName] as JsonObject;
        var typeId = StringOf(typeObject?[IdName]);
        var known = FakeIssueTypes.Find(projectKey, typeId, StringOf(typeObject?[NameName]));
        var typeName = known?.Name ?? StringOf(typeObject?[NameName]);
        var summary = StringOf(fields[SummaryName]);
        var parentKey = StringOf((fields[ParentName] as JsonObject)?[KeyName]);

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

            if (known is { IsSubtask: true } && parentKey is null)
            {
                return Reply(BadRequest, MissingParentFixture);
            }

            if (known is { TakesParent: false } && parentKey is not null)
            {
                return Reply(BadRequest, InvalidParentFixture);
            }

            var number = _nextNumber[projectKey]++;
            var key = $"{projectKey}-{number.ToString(CultureInfo.InvariantCulture)}";
            var stored = new StoredIssue(key, (_nextId++).ToString(CultureInfo.InvariantCulture), true)
            {
                IssueType = typeName,
                IssueTypeInfo = known,
            };

            foreach (var field in fields.Where(field => !IsSetByCreate(field.Key)))
            {
                stored.Fields[field.Key] = field.Value?.DeepClone();
            }

            stored.Fields[SummaryName] = JsonValue.Create(summary);
            stored.Fields[LabelsName] = fields[LabelsName]?.DeepClone() ?? new JsonArray();

            if (parentKey is not null)
            {
                stored.Fields[ParentName] = new JsonObject { [KeyName] = parentKey };
            }

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
        var operations = LabelOperationsOf(body);

        if (fields is null && operations is null)
        {
            // Only add and remove on labels are modelled of the "update" shape; say so instead of "summary required".
            return Reply(BadRequest, body.Contains("\"update\"", StringComparison.Ordinal) ? UnknownFieldFixture : MissingFieldsFixture);
        }

        fields ??= [];

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
                var value = field.Key == PriorityName ? PriorityNode(field.Value) : field.Value?.DeepClone();

                stored.Fields[field.Key] = value;

                if (field.Key == LabelsName)
                {
                    stored.LabelOperations.Clear();
                }
            }

            stored.LabelOperations.AddRange(operations ?? []);

            return Empty(NoContent);
        }
    }

    /// <summary>
    /// <c>PUT issue/{key}/assignee</c>: an account id assigns (the display name is the recorded account's when the id
    /// is its, else the id itself), a null id or the automatic <c>-1</c> unassigns.
    /// </summary>
    public FakeReply Assign(string key, string body)
    {
        if (!TryReadAccountId(body, out var accountId))
        {
            return Reply(BadRequest, MissingFieldsFixture);
        }

        lock (_lock)
        {
            if (Find(key) is not { } stored)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            stored.Fields[AssigneeName] = AssigneeNode(accountId);

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

    public FakeReply EditComment(string key, string commentId, string body, byte[]? recorded = null)
    {
        var comment = ReadCommentBody(body);

        lock (_lock)
        {
            var entry = FindComment(key, commentId, recorded);

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

    public FakeReply DeleteComment(string key, string commentId, byte[]? recorded = null)
    {
        lock (_lock)
        {
            if (Find(key) is not { } stored)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            if (FindComment(key, commentId, recorded) is not { } entry)
            {
                return Reply(NotFound, NotFoundFixture);
            }

            if (!stored.Comments.Remove(entry))
            {
                stored.EditedRecordedComments.Remove(commentId);
                stored.DeletedRecordedComments.Add(commentId);
            }

            return Empty(NoContent);
        }
    }

    /// <summary>The comments list: the recorded one (or an empty one) with its edits and deletes, plus the comments added through the fake.</summary>
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

            for (var index = list.Count - 1; index >= 0; index--)
            {
                var id = list[index]?[IdName]?.GetValue<string>();

                if (id is null)
                {
                    continue;
                }

                if (stored.DeletedRecordedComments.Contains(id))
                {
                    list.RemoveAt(index);
                }
                else if (stored.EditedRecordedComments.TryGetValue(id, out var edited))
                {
                    list[index] = edited.DeepClone();
                }
            }

            foreach (var entry in stored.Comments)
            {
                list.Add(entry.DeepClone());
            }

            node[CommentsName] = list;
            node[TotalName] = list.Count;

            return Encoding.UTF8.GetBytes(node.ToJsonString());
        }
    }

    /// <summary>The reply of a board or backlog list with the rank calls laid over its order.</summary>
    public FakeReply Ranked(FakeReply reply)
    {
        return _order.Apply(reply);
    }

    public FakeReply Rank(string body)
    {
        var keys = ReadRankKeys(body);

        lock (_lock)
        {
            if (keys.Count == 0 || keys.Any(key => Find(key) is null && !_isListedIssue(key)))
            {
                return Reply(BadRequest, MissingRankIssueFixture);
            }

            _order.Record(body);

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

    /// <summary>
    /// The add and remove operations on labels of an <c>update</c> body, in order; null when the body has no
    /// <c>update</c> or it names a field other than labels or an operation other than add and remove (a 400 then).
    /// </summary>
    private static List<LabelOperation>? LabelOperationsOf(string body)
    {
        try
        {
            var update = (JsonNode.Parse(body) as JsonObject)?[UpdateName] as JsonObject;

            if (update is null || update.Any(entry => entry.Key != LabelsName) || update[LabelsName] is not JsonArray list)
            {
                return null;
            }

            var result = new List<LabelOperation>();

            foreach (var entry in list.OfType<JsonObject>())
            {
                var isAdd = entry.ContainsKey(AddName);
                var label = StringOf(entry[isAdd ? AddName : RemoveName]);

                if (label is null || entry.Count != 1 || !(isAdd || entry.ContainsKey(RemoveName)))
                {
                    return null;
                }

                result.Add(new LabelOperation(isAdd, label));
            }

            return result;
        }
        catch (JsonException)
        {
            return null;
        }
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

    private static bool TryReadAccountId(string body, out string? accountId)
    {
        accountId = null;

        try
        {
            if (JsonNode.Parse(body) is not JsonObject { } parsed || !parsed.ContainsKey(AccountIdName))
            {
                return false;
            }

            accountId = StringOf(parsed[AccountIdName]);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private JsonObject? AssigneeNode(string? accountId)
    {
        if (accountId is null or AutomaticAccountId)
        {
            return null;
        }

        var recorded = JsonNode.Parse(_readFixture(MyselfFixture))!;
        var isMe = StringOf(recorded[AccountIdName]) == accountId;
        var displayName = isMe ? StringOf(recorded[DisplayNameName]) : accountId;

        return new JsonObject { [AccountIdName] = accountId, [DisplayNameName] = displayName };
    }

    // A priority is written by id; the issue then shows its name, as Jira's reply does.
    private static JsonNode? PriorityNode(JsonNode? value)
    {
        var id = StringOf((value as JsonObject)?[IdName]);

        if (id is null || !_priorityNames.TryGetValue(id, out var name))
        {
            return value?.DeepClone();
        }

        return new JsonObject { [IdName] = id, [NameName] = name };
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

        ApplyLabelOperations(fields, stored);

        if (stored.Status is not null)
        {
            fields[StatusName] = stored.Status.DeepClone();
        }
    }

    private static void ApplyLabelOperations(JsonObject fields, StoredIssue stored)
    {
        if (stored.LabelOperations.Count == 0)
        {
            return;
        }

        var labels = (fields[LabelsName] as JsonArray)?.Select(StringOf).OfType<string>().ToList() ?? [];

        foreach (var operation in stored.LabelOperations)
        {
            if (operation.IsAdd && !labels.Contains(operation.Label))
            {
                labels.Add(operation.Label);
            }
            else if (!operation.IsAdd)
            {
                labels.Remove(operation.Label);
            }
        }

        fields[LabelsName] = new JsonArray([.. labels.Select(label => (JsonNode?)JsonValue.Create(label))]);
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

    /// <summary>A comment added through the fake, or a recorded one (edited in a copy kept in the edit layer).</summary>
    private JsonObject? FindComment(string key, string commentId, byte[]? recorded)
    {
        if (Find(key) is not { } stored)
        {
            return null;
        }

        if (stored.Comments.FirstOrDefault(entry => entry[IdName]?.GetValue<string>() == commentId) is { } added)
        {
            return added;
        }

        if (stored.DeletedRecordedComments.Contains(commentId))
        {
            return null;
        }

        if (stored.EditedRecordedComments.TryGetValue(commentId, out var edited))
        {
            return edited;
        }

        var list = recorded is null ? null : JsonNode.Parse(recorded)?[CommentsName] as JsonArray;
        var original = list?.FirstOrDefault(entry => entry?[IdName]?.GetValue<string>() == commentId) as JsonObject;

        if (original is null)
        {
            return null;
        }

        var copy = (JsonObject)original.DeepClone();
        stored.EditedRecordedComments[commentId] = copy;

        return copy;
    }

    private static bool IsSetByCreate(string fieldId)
    {
        return fieldId is ProjectName or IssueTypeName or SummaryName or LabelsName or ParentName;
    }

    private static JsonObject IssueTypeNode(StoredIssue stored)
    {
        var node = new JsonObject { [NameName] = stored.IssueType };

        if (stored.IssueTypeInfo is { } info)
        {
            node[IdName] = info.Id;
            node["subtask"] = info.IsSubtask;
            node["hierarchyLevel"] = info.HierarchyLevel;
        }

        return node;
    }

    private JsonObject CreatedIssueNode(StoredIssue stored)
    {
        var status = stored.Status ?? StatusAfter(stored.Key, InitialStatusTransitionId);
        var projectKey = stored.Key[..stored.Key.IndexOf('-')];
        var fields = new JsonObject
        {
            [IssueTypeName] = IssueTypeNode(stored),
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
