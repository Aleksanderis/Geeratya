using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>
/// The Scrum side of the fake: boards 5 (<c>GTS</c>, team-managed) and 6 (<c>GCS</c>, company-managed), their sprints,
/// sprint issues and backlog, built from the recorded survey fixtures (#172) and kept in memory, so sprint moves,
/// start and complete show up in later reads. A Kanban board answers the recorded "does not support sprints" 400.
/// Nothing leaves the process and a restart forgets every change.
/// </summary>
public sealed partial class FakeScrumStore
{
    private const string AgilePrefix = "/rest/agile/1.0/";
    private const string IssuePrefix = "/rest/api/3/issue/";
    private const string BoardPart = "board";
    private const string SprintPart = "sprint";
    private const string IssuePart = "issue";
    private const string BacklogPart = "backlog";
    private const string ConfigurationPart = "configuration";
    private const string EpicPart = "epic";
    private const string Get = "GET";
    private const string Post = "POST";
    private const string Put = "PUT";
    private const string Delete = "DELETE";
    private const string SprintFieldName = "customfield_10020";
    private const string FieldsName = "fields";
    private const string IssuesName = "issues";
    private const string ValuesName = "values";
    private const string StateName = "state";
    private const string IdName = "id";
    private const string NameName = "name";
    private const string GoalName = "goal";
    private const string KeyName = "key";
    private const string StartDateName = "startDate";
    private const string EndDateName = "endDate";
    private const string CompleteDateName = "completeDate";
    private const string CreatedDateName = "createdDate";
    private const string OriginBoardName = "originBoardId";
    private const string BoardIdName = "boardId";
    private const string SelfName = "self";
    private const string StatusName = "status";
    private const string StatusCategoryName = "statusCategory";
    private const string MoveIssuesToName = "moveIssuesTo";
    private const string DoneCategory = "done";
    private const string ActiveState = "active";
    private const string FutureState = "future";
    private const string ClosedState = "closed";
    private const string OpenSprintsJql = "openSprints()";
    private const string FutureSprintsJql = "futureSprints()";
    private const string EmptySprintJql = "is EMPTY";
    private const string SprintSelf = "https://example.atlassian.net/rest/agile/1.0/sprint/";
    private const string MaxResultsName = "maxResults";
    private const string StartAtName = "startAt";
    private const string TotalName = "total";
    private const string IsLastName = "isLast";
    private const string ErrorMessagesName = "errorMessages";
    private const string ExpandName = "expand";
    private const string IssueExpand = "schema,names";
    private const string JqlName = "jql";
    private const string StateFilterName = "state";
    private const string ProjectFilterName = "projectKeyOrId";
    private const string TypeFilterName = "type";
    private const int FirstCreatedSprintId = 1000;
    private const int SprintPageCap = 50;
    private const int BoardDefaultPage = 100;
    private const int Ok = 200;
    private const int Created = 201;
    private const int NoContent = 204;
    private const int BadRequest = 400;
    private const int NotFound = 404;

    private const string BoardListFixture = "scrum/board-list-all.json";
    private const string KanbanSprintsFixture = "scrum/error-sprints-gcm-kanban-400.json";
    private const string BoardMissingFixture = "scrum/error-sprints-board-404.json";
    private const string SprintMissingFixture = "scrum/error-sprint-404.json";
    private const string StateInvalidFixture = "scrum/error-sprints-state-invalid-400.json";
    private const string MoveIntoClosedFixture = "scrum/error-sprint-move-into-closed-400.json";
    private const string StartDateFixture = "scrum/error-sprint-complete-future-no-start-400.json";
    private const string FutureToClosedFixture = "scrum/error-sprint-future-to-closed-400.json";
    private const string ReopenFixture = "scrum/error-sprint-reopen-closed-400.json";
    private const string InvalidPayloadFixture = "scrum/error-sprint-complete-moveissuesto-400.json";
    private const string MissingIssueFixture = "boards/rank-400-missing-issue.json";
    private const string BacklogFixture = "scrum/backlog-gts.json";

    private static readonly FakeScrumBoard[] _boards =
    [
        new(
            5,
            "scrum/board-gts.json",
            "scrum/board-gts-config.json",
            "scrum/board-issues-gts.json",
            "scrum/epics-gts.json",
            "scrum/sprints-gts.json"),
        new(
            6,
            "scrum/board-gcs.json",
            "scrum/board-gcs-config.json",
            "scrum/board-issues-gcs.json",
            "scrum/epics-gcs.json",
            "scrum/sprints-gcs.json"),
    ];

    private static readonly int[] _kanbanBoards = [3, 4];

    private readonly Func<string, byte[]> _readFixture;
    private readonly Lock _gate = new();
    private readonly List<JsonObject> _sprints = [];
    private readonly Dictionary<string, JsonObject> _issues = [];
    private readonly Dictionary<int, List<string>> _members = [];
    private readonly Dictionary<int, List<string>> _boardIssueKeys = [];
    private bool _loaded;
    private int _nextSprintId = FirstCreatedSprintId;

    private readonly GeneratedScrum? _generated;

    public FakeScrumStore(Func<string, byte[]> readFixture, int generatedIssues = 0, bool noActiveSprint = false)
    {
        _readFixture = readFixture;
        _generated = generatedIssues > 0 ? new GeneratedScrum(generatedIssues, readFixture, noActiveSprint) : null;
    }

    /// <summary>The reply for a request on a Scrum or sprint route, or null when the path is not one of them.</summary>
    public FakeReply? TryRoute(string method, string path, IReadOnlyDictionary<string, string> query, string body)
    {
        if (method == Get && path.StartsWith(IssuePrefix, StringComparison.Ordinal))
        {
            return TryIssue(path[IssuePrefix.Length..]);
        }

        if (!path.StartsWith(AgilePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var parts = path[AgilePrefix.Length..].Split('/');

        if (method == Get && _generated?.Route(parts, query) is { } generated)
        {
            return generated;
        }

        if (method == Post && _generated?.Move(method, parts, body) is { } moved)
        {
            return moved;
        }

        lock (_gate)
        {
            Load();

            return parts[0] switch
            {
                BoardPart => RouteBoard(method, parts, query),
                SprintPart => RouteSprint(method, parts, query, body),
                BacklogPart when method == Post && parts is [_, IssuePart] => MoveToBacklog(body),
                _ => null,
            };
        }
    }

    /// <summary>True for an issue on a Scrum board (recorded or generated), so it can be ranked.</summary>
    public bool Knows(string key)
    {
        if (_generated?.Has(key) == true)
        {
            return true;
        }

        lock (_gate)
        {
            Load();

            return _issues.ContainsKey(key);
        }
    }

    /// <summary>One Scrum issue with its current Sprint field; null for any other key (the other routes decide).</summary>
    private FakeReply? TryIssue(string key)
    {
        lock (_gate)
        {
            Load();

            return _issues.ContainsKey(key) ? Json(Ok, IssueView(key)) : null;
        }
    }

    private static FakeReply Json(int status, JsonNode node)
    {
        return new FakeReply(status, Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    private static int IntOf(IReadOnlyDictionary<string, string> query, string name, int fallback)
    {
        var value = 0;
        var parsed = query.TryGetValue(name, out var text)
            && int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        return parsed ? value : fallback;
    }

    private static bool IsDone(JsonObject issue)
    {
        return issue[FieldsName]?[StatusName]?[StatusCategoryName]?[KeyName]?.GetValue<string>() == DoneCategory;
    }

    private static string KeyOf(JsonNode? issue) => issue![KeyName]!.GetValue<string>();

    private static int IdOf(JsonNode? sprint) => sprint![IdName]!.GetValue<int>();

    private static string StateOf(JsonNode? sprint) => sprint![StateName]!.GetValue<string>();

    private static string Now()
    {
        return DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    }

    private FakeReply Raw(int status, string fixture)
    {
        // The reader is readonly and thread-safe, so reading it inside and outside the gate is fine.
        // ReSharper disable once InconsistentlySynchronizedField
        return new FakeReply(status, _readFixture(fixture));
    }

    private JsonObject ParseFixture(string fixture) => JsonNode.Parse(_readFixture(fixture))!.AsObject();

    private JsonObject? FindSprint(int id) => _sprints.Find(sprint => IdOf(sprint) == id);

    private bool IsOpen(int id) => StateOf(FindSprint(id)) != ClosedState;

    private void Load()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;
        var listed = ParseFixture(BacklogFixture)[IssuesName]!.AsArray().Select(KeyOf).ToHashSet();

        foreach (var board in _boards)
        {
            LoadSprints(board);
            LoadIssues(board, listed);
        }
    }

    private void LoadSprints(FakeScrumBoard board)
    {
        foreach (var sprint in ParseFixture(board.SprintsFixture)[ValuesName]!.AsArray())
        {
            _sprints.Add(sprint!.AsObject());
            _members[IdOf(sprint)] = [];
        }
    }

    private void LoadIssues(FakeScrumBoard board, HashSet<string> backlogKeys)
    {
        var keys = new List<string>();
        _boardIssueKeys[board.Id] = keys;

        foreach (var node in ParseFixture(board.IssuesFixture)[IssuesName]!.AsArray())
        {
            var issue = node!.AsObject();
            var key = KeyOf(issue);
            _issues[key] = issue;
            keys.Add(key);

            // An open sprint the issue lists but the sprint endpoint does not (GTS-35, see jira-layer.md) is backlog.
            foreach (var sprint in issue[FieldsName]![SprintFieldName]?.AsArray() ?? [])
            {
                var id = IdOf(sprint);

                if (!backlogKeys.Contains(key) || !IsOpen(id))
                {
                    _members[id].Add(key);
                }
            }
        }
    }
}
