using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Geeratya.FakeJira;

public sealed partial class FakeScrumStore
{
    [GeneratedRegex(@"sprint\s*=\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex SprintEqualsJql();

    private FakeReply? RouteBoard(string method, string[] parts, IReadOnlyDictionary<string, string> query)
    {
        if (method != Get)
        {
            return null;
        }

        if (parts.Length == 1)
        {
            return BoardList(query);
        }

        var scrum = FindBoard(parts[1]);
        var section = parts.Length > 2 ? parts[2] : string.Empty;

        if (scrum is null)
        {
            return section == SprintPart ? KanbanOrMissing(parts[1]) : null;
        }

        return (parts.Length, section) switch
        {
            (2, _) => Raw(Ok, scrum.BoardFixture),
            (3, ConfigurationPart) => Raw(Ok, scrum.ConfigurationFixture),
            (3, QuickFilterPart) => Raw(Ok, scrum.Id == 6 ? QuickFiltersWithFixture : QuickFiltersNoneFixture),
            (3, EpicPart) => Raw(Ok, scrum.EpicsFixture),
            (3, SprintPart) => SprintList(scrum, query),
            (3, IssuePart) => BoardIssues(scrum, query),
            (3, BacklogPart) => Backlog(scrum, query),
            (5, SprintPart) when parts[4] == IssuePart => SprintIssues(parts[3], query),
            _ => null,
        };
    }

    private FakeReply KanbanOrMissing(string text)
    {
        var kanban = int.TryParse(text, out var id) && _kanbanBoards.Contains(id);

        return kanban ? Raw(BadRequest, KanbanSprintsFixture) : Raw(NotFound, BoardMissingFixture);
    }

    private static FakeScrumBoard? FindBoard(string text)
    {
        var parsed = int.TryParse(text, out var id);

        return parsed ? Array.Find(_boards, board => board.Id == id) : null;
    }

    /// <summary>The recorded list of all four boards, narrowed by <c>projectKeyOrId</c> and <c>type</c>.</summary>
    private FakeReply BoardList(IReadOnlyDictionary<string, string> query)
    {
        var list = ParseFixture(BoardListFixture);
        var kept = new JsonArray();

        foreach (var board in list[ValuesName]!.AsArray())
        {
            var projectKey = board!["location"]?["projectKey"]?.GetValue<string>();
            var type = board[TypeFilterName]?.GetValue<string>();
            var projectOk = !query.TryGetValue(ProjectFilterName, out var wanted) || wanted == projectKey;
            var typeOk = !query.TryGetValue(TypeFilterName, out var wantedType) || wantedType == type;

            if (projectOk && typeOk)
            {
                kept.Add(board.DeepClone());
            }
        }

        if (_generated is not null)
        {
            var generated = _generated.BoardEntry();
            var type = generated[TypeFilterName]!.GetValue<string>();
            var projectOk = !query.TryGetValue(ProjectFilterName, out var wanted) || wanted == "GEN";
            var typeOk = !query.TryGetValue(TypeFilterName, out var wantedType) || wantedType == type;

            if (projectOk && typeOk)
            {
                kept.Add((JsonNode)generated);
            }
        }

        list[TotalName] = kept.Count;
        list[ValuesName] = kept;

        return Json(Ok, list);
    }

    /// <summary>Sprints of the board: closed first, then active, then future, filtered by <c>state</c>.</summary>
    private FakeReply SprintList(FakeScrumBoard board, IReadOnlyDictionary<string, string> query)
    {
        var states = query.TryGetValue(StateFilterName, out var text) ? text.Split(',') : [];

        if (states.Any(state => state is not (ActiveState or FutureState or ClosedState)))
        {
            var error = ParseFixture(StateInvalidFixture);
            error[ErrorMessagesName] = new JsonArray($"Illegal value {states.First(Unknown)} for param state.");

            return Json(BadRequest, error);
        }

        var matching = _sprints
            .Where(sprint => sprint[OriginBoardName]!.GetValue<int>() == board.Id)
            .Where(sprint => states.Length == 0 || states.Contains(StateOf(sprint)))
            .OrderBy(StateRank)
            .ToList();
        var start = Math.Max(IntOf(query, StartAtName, 0), 0);
        var size = Math.Clamp(IntOf(query, MaxResultsName, SprintPageCap), 1, SprintPageCap);
        var values = new JsonArray();

        foreach (var sprint in matching.Skip(start).Take(size))
        {
            values.Add(sprint.DeepClone());
        }

        var page = new JsonObject
        {
            [IsLastName] = start + size >= matching.Count,
            [MaxResultsName] = size,
            [StartAtName] = start,
            [TotalName] = matching.Count,
            [ValuesName] = values,
        };

        return Json(Ok, page);
    }

    private static bool Unknown(string state) => state is not (ActiveState or FutureState or ClosedState);

    private static int StateRank(JsonObject sprint)
    {
        return StateOf(sprint) switch
        {
            ClosedState => 0,
            ActiveState => 1,
            _ => 2,
        };
    }

    private FakeReply BoardIssues(FakeScrumBoard board, IReadOnlyDictionary<string, string> query)
    {
        var keys = _boardIssueKeys[board.Id].AsEnumerable();

        if (query.TryGetValue(JqlName, out var jql))
        {
            keys = keys.Where(key => InJql(key, jql));
        }

        return IssuePage([.. keys], query, BoardDefaultPage);
    }

    /// <summary>
    /// The backlog: board issues in no open sprint that are not done (so unfinished issues of a completed sprint
    /// are back in it), as the recorded backlog of both boards shows.
    /// </summary>
    private FakeReply Backlog(FakeScrumBoard board, IReadOnlyDictionary<string, string> query)
    {
        var keys = _boardIssueKeys[board.Id]
            .Where(key => !InOpenSprint(key) && !IsDone(_issues[key]))
            .ToList();

        return IssuePage(keys, query, BoardDefaultPage);
    }

    private FakeReply SprintIssues(string idText, IReadOnlyDictionary<string, string> query)
    {
        if (!int.TryParse(idText, out var id) || FindSprint(id) is null)
        {
            return Raw(NotFound, SprintMissingFixture);
        }

        return IssuePage(_members[id], query, SprintPageCap);
    }

    private FakeReply IssuePage(
        List<string> keys,
        IReadOnlyDictionary<string, string> query,
        int defaultSize)
    {
        var start = Math.Max(IntOf(query, StartAtName, 0), 0);
        var size = Math.Max(IntOf(query, MaxResultsName, defaultSize), 1);
        var issues = new JsonArray();

        foreach (var key in keys.Skip(start).Take(size))
        {
            issues.Add((JsonNode)IssueView(key));
        }

        var page = new JsonObject
        {
            [ExpandName] = IssueExpand,
            [StartAtName] = start,
            [MaxResultsName] = size,
            [TotalName] = keys.Count,
            [IssuesName] = issues,
        };

        return Json(Ok, page);
    }

    /// <summary>
    /// The detail of a recorded Scrum board issue (<c>GCS-n</c>, <c>GTS-n</c>) as JSON bytes, with the
    /// <c>Sprint</c> field following the current memberships; null for any other key.
    /// </summary>
    public byte[]? IssueDetail(string key)
    {
        lock (_gate)
        {
            Load();

            if (!_issues.ContainsKey(key))
            {
                return null;
            }

            return Encoding.UTF8.GetBytes(IssueView(key).ToJsonString());
        }
    }

    /// <summary>The issue with its <c>Sprint</c> field rebuilt from the current memberships (null when none).</summary>
    private JsonObject IssueView(string key)
    {
        var copy = _issues[key].DeepClone().AsObject();
        var listed = _sprints
            .Where(sprint => _members[IdOf(sprint)].Contains(key))
            .OrderBy(sprint => StateOf(sprint) == ClosedState ? 0 : 1)
            .ToList();
        JsonNode? field = null;

        if (listed.Count > 0)
        {
            var array = new JsonArray();

            foreach (var sprint in listed)
            {
                array.Add((JsonNode)FieldSprint(sprint));
            }

            field = array;
        }

        copy[FieldsName]![SprintFieldName] = field;

        return copy;
    }

    private static JsonObject FieldSprint(JsonObject sprint)
    {
        var result = new JsonObject
        {
            [IdName] = IdOf(sprint),
            [NameName] = sprint[NameName]!.GetValue<string>(),
            [StateName] = StateOf(sprint),
            [BoardIdName] = sprint[OriginBoardName]!.GetValue<int>(),
        };

        foreach (var name in new[] { StartDateName, EndDateName, CompleteDateName, GoalName })
        {
            if (sprint[name] is { } value)
            {
                result[name] = value.DeepClone();
            }
        }

        return result;
    }

    private bool InOpenSprint(string key)
    {
        return _sprints.Any(sprint => StateOf(sprint) != ClosedState && _members[IdOf(sprint)].Contains(key));
    }

    private bool InJql(string key, string jql)
    {
        if (!InSprintJql(key, jql))
        {
            return false;
        }

        return !FakeQuickFilters.Applies(jql)
            || FakeQuickFilters.Matches(_issues[key], jql, MyAccountId(), DateTimeOffset.UtcNow);
    }

    private string MyAccountId()
    {
        return ParseFixture(MyselfFixture)["accountId"]!.GetValue<string>();
    }

    private bool InSprintJql(string key, string jql)
    {
        if (jql.Contains(FutureSprintsJql, StringComparison.OrdinalIgnoreCase)
            && jql.Contains(EmptySprintJql, StringComparison.OrdinalIgnoreCase))
        {
            // The Scrum backlog view's query: open and future sprints plus issues in no sprint at all (#506).
            return InOpenSprint(key) || !_sprints.Any(sprint => _members[IdOf(sprint)].Contains(key));
        }

        if (jql.Contains(OpenSprintsJql, StringComparison.OrdinalIgnoreCase))
        {
            return _sprints.Any(sprint => StateOf(sprint) == ActiveState && _members[IdOf(sprint)].Contains(key));
        }

        if (SprintEqualsJql().Match(jql) is { Success: true } match)
        {
            var id = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);

            return _members.TryGetValue(id, out var members) && members.Contains(key);
        }

        return true;
    }
}
