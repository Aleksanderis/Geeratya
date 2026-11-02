using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

public sealed partial class FakeScrumStore
{
    private FakeReply? RouteSprint(
        string method,
        string[] parts,
        IReadOnlyDictionary<string, string> query,
        string body)
    {
        if (parts.Length == 1)
        {
            return method == Post ? CreateSprint(body) : null;
        }

        if (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            || FindSprint(id) is not { } sprint)
        {
            return Raw(NotFound, SprintMissingFixture);
        }

        return (method, parts.Length) switch
        {
            (Get, 2) => Json(Ok, sprint.DeepClone()),
            (Put, 2) => Update(sprint, body, true),
            (Post, 2) => Update(sprint, body, false),
            (Delete, 2) => DeleteSprint(id),
            (Get, 3) when parts[2] == IssuePart => SprintIssues(parts[1], query),
            (Post, 3) when parts[2] == IssuePart => MoveIssues(id, body),
            _ => null,
        };
    }

    private static JsonObject? ParseObject(string body)
    {
        try
        {
            return JsonNode.Parse(body) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TextOf(JsonObject body, string name)
    {
        return body[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
    }

    private FakeReply CreateSprint(string body)
    {
        var request = ParseObject(body);
        var name = request is null ? null : TextOf(request, NameName);
        var board = request?[OriginBoardName] is JsonValue origin && origin.TryGetValue<int>(out var id) ? id : 0;

        if (string.IsNullOrWhiteSpace(name) || Array.TrueForAll(_boards, candidate => candidate.Id != board))
        {
            return Raw(BadRequest, InvalidPayloadFixture);
        }

        var sprint = new JsonObject
        {
            [CreatedDateName] = Now(),
            [IdName] = _nextSprintId,
            [NameName] = name,
            [OriginBoardName] = board,
            [SelfName] = $"{SprintSelf}{_nextSprintId}",
            [StateName] = FutureState,
        };

        if (TextOf(request!, GoalName) is { } goal)
        {
            sprint[GoalName] = goal;
        }

        _sprints.Add(sprint);
        _members[_nextSprintId] = [];
        _nextSprintId++;

        return Json(Created, sprint.DeepClone());
    }

    /// <summary>
    /// <c>PUT</c> (replace: an omitted goal is dropped) and <c>POST</c> (partial) on a sprint, with the recorded state
    /// rules: start needs both dates, future to closed and closed to active are 400, <c>moveIssuesTo</c> is 400.
    /// </summary>
    private FakeReply Update(JsonObject sprint, string body, bool replace)
    {
        var request = ParseObject(body);

        if (request is null || request.ContainsKey(MoveIssuesToName))
        {
            return Raw(BadRequest, InvalidPayloadFixture);
        }

        var current = StateOf(sprint);
        var target = TextOf(request, StateName) ?? current;

        if (Unknown(target))
        {
            return Raw(BadRequest, InvalidPayloadFixture);
        }

        var failure = CheckTransition(sprint, request, current, target);

        if (failure is not null)
        {
            return failure;
        }

        if (TextOf(request, NameName) is { Length: > 0 } name)
        {
            sprint[NameName] = name;
        }

        if (replace || request.ContainsKey(GoalName))
        {
            sprint.Remove(GoalName);
        }

        CopyText(request, sprint, GoalName);
        CopyText(request, sprint, StartDateName);
        CopyText(request, sprint, EndDateName);

        if (current == ActiveState && target == ClosedState)
        {
            sprint[CompleteDateName] = Now();
        }

        sprint[StateName] = target;

        return Json(Ok, sprint.DeepClone());
    }

    private FakeReply? CheckTransition(JsonObject sprint, JsonObject request, string current, string target)
    {
        if (current == FutureState && target == ClosedState)
        {
            return Raw(BadRequest, FutureToClosedFixture);
        }

        if (current == ClosedState && target == ActiveState)
        {
            return Raw(BadRequest, ReopenFixture);
        }

        var starts = current == FutureState && target == ActiveState;
        var hasDates = (TextOf(request, StartDateName) ?? TextOf(sprint, StartDateName)) is not null
            && (TextOf(request, EndDateName) ?? TextOf(sprint, EndDateName)) is not null;

        return starts && !hasDates ? Raw(BadRequest, StartDateFixture) : null;
    }

    private static void CopyText(JsonObject from, JsonObject to, string name)
    {
        if (TextOf(from, name) is { } text)
        {
            to[name] = text;
        }
    }

    private FakeReply DeleteSprint(int id)
    {
        _sprints.RemoveAll(sprint => IdOf(sprint) == id);
        _members.Remove(id);

        return new FakeReply(NoContent, []);
    }

    /// <summary>Moves issues into an open sprint; an issue in another open sprint leaves it.</summary>
    private FakeReply MoveIssues(int id, string body)
    {
        if (!IsOpen(id))
        {
            return Raw(BadRequest, MoveIntoClosedFixture);
        }

        var keys = KeysOf(body);

        if (keys is null || keys.Any(key => !SameBoard(id, key)))
        {
            return Raw(BadRequest, MissingIssueFixture);
        }

        foreach (var key in keys)
        {
            LeaveOpenSprints(key);

            if (!_members[id].Contains(key))
            {
                _members[id].Add(key);
            }
        }

        return new FakeReply(NoContent, []);
    }

    private FakeReply MoveToBacklog(string body)
    {
        var keys = KeysOf(body);

        if (keys is null)
        {
            return Raw(BadRequest, MissingIssueFixture);
        }

        foreach (var key in keys)
        {
            LeaveOpenSprints(key);
        }

        return new FakeReply(NoContent, []);
    }

    private bool SameBoard(int sprintId, string key)
    {
        var board = FindSprint(sprintId)![OriginBoardName]!.GetValue<int>();

        return _boardIssueKeys[board].Contains(key);
    }

    private void LeaveOpenSprints(string key)
    {
        foreach (var sprint in _sprints.Where(candidate => StateOf(candidate) != ClosedState))
        {
            _members[IdOf(sprint)].Remove(key);
        }
    }

    /// <summary>The keys of <c>{"issues": [...]}</c>; null when empty, malformed or naming an unknown issue.</summary>
    private List<string>? KeysOf(string body)
    {
        if (ParseObject(body)?[IssuesName] is not JsonArray array || array.Count == 0)
        {
            return null;
        }

        var keys = new List<string>();

        foreach (var node in array)
        {
            if (node is not JsonValue value || !value.TryGetValue<string>(out var key) || !_issues.ContainsKey(key))
            {
                return null;
            }

            keys.Add(key);
        }

        return keys;
    }
}
