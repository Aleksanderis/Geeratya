using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Geeratya.FakeJira;

/// <summary>
/// Serves the invented demo dataset (<see cref="DemoDataset"/>) as a company-managed Scrum project with the Jira calls
/// the app makes for boards, sprints, the backlog, search, issue details, comments, edits and transitions. State is
/// in memory, so an edit or a transition shows up in later reads. Any other request is left to the recorded fake.
/// </summary>
public sealed partial class DemoJira
{
    private const string Get = "GET";
    private const string Post = "POST";
    private const string Put = "PUT";
    private const string Api = "/rest/api/3/";
    private const string Agile = "/rest/agile/1.0/";
    private const string Values = "values";
    private const int DefaultPage = 50;
    private static readonly int[] _sprintNumbers = [14, 15, 16];
    private static readonly string[] _statuses = ["To Do", "In Progress", "In Review", "Done"];

    private readonly Lock _gate = new();
    private readonly List<DemoIssue> _ranked = DemoDataset.Issues();

    [GeneratedRegex(@"^issue/(WAY-\d+)(?:/([a-z]+)(?:/(\d+))?)?$")]
    private static partial Regex IssuePath();

    /// <summary>The reply for a demo request; null when the request is not the demo's (the recorded fake answers).</summary>
    public FakeReply? TryRoute(string method, string path, IReadOnlyDictionary<string, string> query, string body)
    {
        lock (_gate)
        {
            if (path.StartsWith(Agile, StringComparison.Ordinal))
            {
                return Agile1(method, path[Agile.Length..], query, body);
            }

            if (!path.StartsWith(Api, StringComparison.Ordinal))
            {
                return null;
            }

            return Api3(method, path[Api.Length..], query, body);
        }
    }

    internal static FakeReply Json(JsonNode node, int status = 200)
    {
        return new FakeReply(status, Encoding.UTF8.GetBytes(node.ToJsonString()));
    }

    internal static int IntOf(IReadOnlyDictionary<string, string> query, string name, int fallback)
    {
        return query.TryGetValue(name, out var text)
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : fallback;
    }

    private FakeReply? Api3(string method, string path, IReadOnlyDictionary<string, string> query, string body)
    {
        switch (method, path)
        {
            case (Get, "myself"):
                return Json(DemoJson.User(DemoDataset.People[0]));
            case (Get, "project/search"):
                return Projects();
            case (Get, "user/assignable/search" or "user/viewissue/search"):
                return People(query);
            case (Get or Post, "search/jql"):
                return Search(method == Post ? BodyParameters(body) : query);
            case (Get or Post, "search/approximate-count"):
                return Json(new JsonObject { ["count"] = _ranked.Count });
        }

        if (IssuePath().Match(path) is not { Success: true } match)
        {
            return null;
        }

        var issue = _ranked.Find(candidate => candidate.Key == match.Groups[1].Value);

        if (issue is null)
        {
            return null;
        }

        return IssueCall(method, issue, match.Groups[2].Value, query, body);
    }

    private FakeReply? IssueCall(
        string method,
        DemoIssue issue,
        string part,
        IReadOnlyDictionary<string, string> query,
        string body)
    {
        return (method, part) switch
        {
            (Get, "") => Json(DemoJson.Issue(issue, _ranked)),
            (Get, "comment") => Comments(issue, query),
            (Get, "remotelink") => RemoteLinks(issue),
            (Get, "transitions") => Transitions(),
            (Put, "") => Edit(issue, body),
            (Put, "assignee") => Assign(issue, body),
            (Post, "transitions") => Transition(issue, body),
            (Post, "comment") => AddComment(issue, body),
            _ => null,
        };
    }

    private FakeReply? Agile1(string method, string path, IReadOnlyDictionary<string, string> query, string body)
    {
        var parts = path.Split('/');

        return (method, parts) switch
        {
            (Get, ["board"]) => BoardList(query),
            (Get, ["board", "20"]) => Json(Board()),
            (Get, ["board", "20", "configuration"]) => Json(Configuration()),
            (Get, ["board", "20", "quickfilter"]) => Json(Page([])),
            (Get, ["board", "20", "epic"]) => Epics(),
            (Get, ["board", "20", "sprint"]) => Sprints(query),
            (Get, ["board", "20", "issue"]) => IssuePage(
                [.. DemoJql.Run(query.GetValueOrDefault("jql"), _ranked).Where(issue => issue.Type != "Epic")],
                query),
            (Get, ["board", "20", "backlog"]) => IssuePage([.. _ranked.Where(IsBacklog)], query),
            (Get, ["sprint", var id]) when int.TryParse(id, out var sprint) && sprint is >= 14 and <= 16
                => Json(DemoJson.Sprint(sprint)),
            (Get, ["sprint", var id, "issue"]) when int.TryParse(id, out var sprint) => IssuePage(
                [.. _ranked.Where(issue => issue.Sprint == sprint)],
                query),
            (Get, ["issue", var key]) => _ranked.Find(issue => issue.Key == key) is { } found
                ? Json(DemoJson.Issue(found, _ranked))
                : null,
            (Post, ["sprint", var id, "issue"]) when int.TryParse(id, out var sprint) => MoveToSprint(sprint, body),
            (Post, ["backlog", "issue"]) => MoveToSprint(0, body),
            (Put, ["issue", "rank"]) => Rank(body),
            _ => null,
        };
    }

    private static bool IsBacklog(DemoIssue issue) => issue.Type != "Epic" && issue.Sprint == 0;

    private static Dictionary<string, string> BodyParameters(string body)
    {
        var result = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(body))
        {
            return result;
        }

        using var document = JsonDocument.Parse(body);

        foreach (var property in document.RootElement.EnumerateObject())
        {
            result[property.Name] = property.Value.ToString();
        }

        return result;
    }

    private FakeReply Search(IReadOnlyDictionary<string, string> parameters)
    {
        var matches = DemoJql.Run(parameters.GetValueOrDefault("jql"), _ranked);
        var size = Math.Min(IntOf(parameters, "maxResults", DefaultPage), 1000);
        var start = IntOf(parameters, "nextPageToken", 0);
        var issues = new JsonArray();

        foreach (var issue in matches.Skip(start).Take(size))
        {
            issues.Add((JsonNode)DemoJson.Issue(issue, _ranked));
        }

        var page = new JsonObject { ["isLast"] = start + size >= matches.Count, ["issues"] = issues };

        if (start + size < matches.Count)
        {
            page["nextPageToken"] = (start + size).ToString(CultureInfo.InvariantCulture);
        }

        return Json(page);
    }

    private FakeReply IssuePage(List<DemoIssue> matches, IReadOnlyDictionary<string, string> query)
    {
        var start = IntOf(query, "startAt", 0);
        var size = Math.Max(1, IntOf(query, "maxResults", DefaultPage));
        var issues = new JsonArray();

        foreach (var issue in matches.Skip(start).Take(size))
        {
            issues.Add((JsonNode)DemoJson.Issue(issue, _ranked));
        }

        return Json(
            new JsonObject
            {
                ["expand"] = "schema,names",
                ["startAt"] = start,
                ["maxResults"] = size,
                ["total"] = matches.Count,
                ["issues"] = issues,
            });
    }

    private static JsonObject Page(JsonArray values)
    {
        return new JsonObject
        {
            ["isLast"] = true,
            ["maxResults"] = DefaultPage,
            ["startAt"] = 0,
            ["total"] = values.Count,
            [Values] = values,
        };
    }

    private static FakeReply Projects()
    {
        var project = new JsonObject
        {
            ["id"] = "10100",
            ["key"] = DemoDataset.ProjectKey,
            ["name"] = "Wayfinder",
            ["projectTypeKey"] = "software",
            ["simplified"] = false,
            ["style"] = "classic",
            ["isPrivate"] = false,
            ["avatarUrls"] = new JsonObject(),
        };

        return Json(Page(new JsonArray(project)));
    }

    private static FakeReply People(IReadOnlyDictionary<string, string> query)
    {
        var text = query.GetValueOrDefault("query", string.Empty).Trim();
        var users = new JsonArray();

        foreach (var person in DemoDataset.People.Where(
            person => person.Name.Contains(text, StringComparison.OrdinalIgnoreCase)))
        {
            users.Add((JsonNode)DemoJson.User(person));
        }

        return Json(users);
    }

    private static JsonObject Board()
    {
        return new JsonObject
        {
            ["id"] = DemoJson.BoardId,
            ["name"] = "Wayfinder board",
            ["type"] = "scrum",
            ["self"] = $"https://wayfinder-demo.example{Agile}board/{DemoJson.BoardId}",
            ["location"] = new JsonObject
            {
                ["projectId"] = 10100,
                ["projectKey"] = DemoDataset.ProjectKey,
                ["projectName"] = "Wayfinder",
                ["displayName"] = "Wayfinder (WAY)",
                ["name"] = "Wayfinder (WAY)",
                ["projectTypeKey"] = "software",
            },
        };
    }

    private static FakeReply BoardList(IReadOnlyDictionary<string, string> query)
    {
        var projectOk = !query.TryGetValue("projectKeyOrId", out var project) || project == DemoDataset.ProjectKey;
        var typeOk = !query.TryGetValue("type", out var type) || type == "scrum";

        var boards = new JsonArray();

        if (projectOk && typeOk)
        {
            boards.Push(Board());
        }

        return Json(Page(boards));
    }

    private static JsonObject Configuration()
    {
        var columns = new JsonArray();

        foreach (var status in _statuses)
        {
            columns.Push(
                new JsonObject
                {
                    ["name"] = status,
                    ["statuses"] = new JsonArray(new JsonObject { ["id"] = DemoJson.StatusId(status) }),
                });
        }

        return new JsonObject
        {
            ["id"] = DemoJson.BoardId,
            ["name"] = "Wayfinder board",
            ["type"] = "scrum",
            ["columnConfig"] = new JsonObject { ["columns"] = columns, ["constraintType"] = "none" },
            ["estimation"] = new JsonObject
            {
                ["type"] = "field",
                ["field"] = new JsonObject { ["displayName"] = "Story Points", ["fieldId"] = DemoJson.StoryPointsField },
            },
            ["location"] = new JsonObject { ["id"] = "10100", ["key"] = DemoDataset.ProjectKey, ["type"] = "project" },
            ["ranking"] = new JsonObject { ["rankCustomFieldId"] = 10019 },
        };
    }

    private FakeReply Epics()
    {
        var epics = new JsonArray();

        foreach (var epic in _ranked.Where(issue => issue.Type == "Epic"))
        {
            epics.Push(
                new JsonObject
                {
                    ["id"] = 20000 + epic.Number,
                    ["key"] = epic.Key,
                    ["name"] = epic.Summary,
                    ["summary"] = epic.Summary,
                    ["done"] = false,
                    ["color"] = new JsonObject { ["key"] = $"color_{epic.Number + 1}" },
                });
        }

        return Json(Page(epics));
    }

    private static FakeReply Sprints(IReadOnlyDictionary<string, string> query)
    {
        var states = query.TryGetValue("state", out var text) ? text.Split(',') : [];
        var values = new JsonArray();

        foreach (var sprint in _sprintNumbers.Where(
            sprint => states.Length == 0 || states.Contains(DemoJql.SprintState(sprint))))
        {
            values.Add((JsonNode)DemoJson.Sprint(sprint));
        }

        return Json(Page(values));
    }

    private static FakeReply Comments(DemoIssue issue, IReadOnlyDictionary<string, string> query)
    {
        var all = issue.Comments.Select((comment, index) => DemoJson.Comment(comment, CommentId(issue, index))).ToList();

        if (query.GetValueOrDefault("orderBy") == "-created")
        {
            all.Reverse();
        }

        var start = IntOf(query, "startAt", 0);
        var size = Math.Max(1, IntOf(query, "maxResults", DefaultPage));
        var page = new JsonArray();

        foreach (var comment in all.Skip(start).Take(size))
        {
            page.Add((JsonNode)comment);
        }

        return Json(
            new JsonObject
            {
                ["startAt"] = start,
                ["maxResults"] = size,
                ["total"] = all.Count,
                ["comments"] = page,
            });
    }

    private static int CommentId(DemoIssue issue, int index) => 50000 + (issue.Number * 100) + index;

    private static FakeReply RemoteLinks(DemoIssue issue)
    {
        if (issue.Number != 20)
        {
            return Json(new JsonArray());
        }

        var link = new JsonObject
        {
            ["id"] = 1,
            ["object"] = new JsonObject
            {
                ["url"] = "https://wiki.wayfinder.example/engineering/booking-email-parser",
                ["title"] = "Booking email parser: design notes",
            },
        };

        return Json(new JsonArray(link));
    }
}
