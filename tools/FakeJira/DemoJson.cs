using System.Globalization;
using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

/// <summary>Builds the Jira-shaped JSON of the demo dataset.</summary>
public static class DemoJson
{
    public const int BoardId = 20;
    public const string StoryPointsField = "customfield_10047";
    public const string SprintField = "customfield_10020";
    public const string Base = "https://wayfinder-demo.example/rest/api/3/";

    private static readonly string[] _priorities = ["Highest", "High", "Medium", "Low", "Lowest"];

    public static string Time(DateTimeOffset moment)
    {
        return moment.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+0000'", CultureInfo.InvariantCulture);
    }

    public static string StatusId(string status)
    {
        return status switch
        {
            "In Progress" => "3",
            "In Review" => "10006",
            "Done" => "10005",
            _ => "10004",
        };
    }

    public static JsonObject User(DemoPerson person)
    {
        return new JsonObject
        {
            ["accountId"] = person.AccountId,
            ["accountType"] = "atlassian",
            ["active"] = true,
            ["avatarUrls"] = new JsonObject(),
            ["displayName"] = person.Name,
            ["emailAddress"] = $"{person.Name.ToLowerInvariant().Replace(' ', '.')}@wayfinder.example",
            ["timeZone"] = "Europe/Lisbon",
        };
    }

    public static JsonObject Status(string status)
    {
        var category = DemoJql.CategoryKey(status);
        var (categoryId, categoryName, colorName) = category switch
        {
            "done" => (3, "Done", "green"),
            "new" => (2, "To Do", "blue-gray"),
            _ => (4, "In Progress", "yellow"),
        };

        return new JsonObject
        {
            ["id"] = StatusId(status),
            ["name"] = status,
            ["description"] = string.Empty,
            ["statusCategory"] = new JsonObject
            {
                ["id"] = categoryId,
                ["key"] = category,
                ["name"] = categoryName,
                ["colorName"] = colorName,
            },
        };
    }

    public static JsonObject Priority(string priority)
    {
        var index = Array.IndexOf(_priorities, priority);

        return new JsonObject { ["id"] = (index + 1).ToString(CultureInfo.InvariantCulture), ["name"] = priority };
    }

    public static JsonObject IssueType(string type)
    {
        var id = type switch { "Epic" => "10000", "Story" => "10008", "Bug" => "10009", _ => "10006" };

        return new JsonObject
        {
            ["id"] = id,
            ["name"] = type,
            ["subtask"] = false,
            ["hierarchyLevel"] = type == "Epic" ? 1 : 0,
        };
    }

    public static JsonObject Sprint(int sprint)
    {
        var now = DateTimeOffset.UtcNow.Date.AddHours(9);
        var start = sprint switch { 14 => now.AddDays(-21), 15 => now.AddDays(-7), _ => now.AddDays(7) };
        var state = DemoJql.SprintState(sprint);
        var json = new JsonObject
        {
            ["id"] = sprint,
            ["self"] = $"{Base}../../agile/1.0/sprint/{sprint}",
            ["state"] = state,
            ["name"] = $"Sprint {sprint}",
            ["startDate"] = Time(start),
            ["endDate"] = Time(start.AddDays(14)),
            ["originBoardId"] = BoardId,
            ["goal"] = sprint switch
            {
                14 => "Region picker and shared trips foundation",
                15 => "Ship offline map downloads to the beta group",
                _ => "Wi-Fi only downloads and hotel email parsing",
            },
        };

        if (state == "closed")
        {
            json["completeDate"] = Time(start.AddDays(14));
        }

        return json;
    }

    public static JsonObject Issue(DemoIssue issue, IReadOnlyList<DemoIssue> all)
    {
        var fields = new JsonObject
        {
            ["summary"] = issue.Summary,
            ["status"] = Status(issue.Status),
            ["issuetype"] = IssueType(issue.Type),
            ["priority"] = Priority(issue.Priority),
            ["assignee"] = issue.Assignee < 0 ? null : User(DemoDataset.People[issue.Assignee]),
            ["reporter"] = User(DemoDataset.People[issue.Reporter]),
            ["labels"] = new JsonArray([.. issue.Labels.Select(label => (JsonNode)label)]),
            ["created"] = Time(DateTimeOffset.UtcNow.AddDays(-issue.CreatedDaysAgo)),
            ["updated"] = Time(issue.Updated),
            ["project"] = new JsonObject { ["key"] = DemoDataset.ProjectKey, ["name"] = "Wayfinder", ["id"] = "10100" },
            ["description"] = issue.Description.DeepClone(),
            ["subtasks"] = new JsonArray(),
            ["attachment"] = new JsonArray(),
            ["issuelinks"] = Links(issue, all),
            [StoryPointsField] = issue.Points,
            [SprintField] = issue.Sprint == 0 ? null : new JsonArray(SprintStub(issue.Sprint)),
        };

        if (issue.Epic != 0)
        {
            fields["parent"] = Stub(all.First(other => other.Number == issue.Epic));
        }

        return new JsonObject
        {
            ["id"] = Id(issue),
            ["key"] = issue.Key,
            ["self"] = $"{Base}issue/{Id(issue)}",
            ["fields"] = fields,
        };
    }

    public static JsonObject Comment(DemoComment comment, int id)
    {
        var moment = Time(DateTimeOffset.UtcNow.AddHours(-comment.HoursAgo));
        var author = User(DemoDataset.People[comment.Author]);

        return new JsonObject
        {
            ["id"] = id.ToString(CultureInfo.InvariantCulture),
            ["author"] = author,
            ["updateAuthor"] = author.DeepClone(),
            ["body"] = DemoAdf.Document(comment.Text),
            ["created"] = moment,
            ["updated"] = moment,
        };
    }

    private static string Id(DemoIssue issue) => (20000 + issue.Number).ToString(CultureInfo.InvariantCulture);

    private static JsonObject SprintStub(int sprint)
    {
        var full = Sprint(sprint);

        return new JsonObject
        {
            ["id"] = sprint,
            ["name"] = full["name"]!.GetValue<string>(),
            ["state"] = full["state"]!.GetValue<string>(),
            ["boardId"] = BoardId,
            ["startDate"] = full["startDate"]!.GetValue<string>(),
            ["endDate"] = full["endDate"]!.GetValue<string>(),
        };
    }

    private static JsonObject Stub(DemoIssue issue)
    {
        return new JsonObject
        {
            ["id"] = Id(issue),
            ["key"] = issue.Key,
            ["fields"] = new JsonObject
            {
                ["summary"] = issue.Summary,
                ["status"] = Status(issue.Status),
                ["priority"] = Priority(issue.Priority),
                ["issuetype"] = IssueType(issue.Type),
            },
        };
    }

    private static JsonArray Links(DemoIssue issue, IReadOnlyList<DemoIssue> all)
    {
        var links = new JsonArray();

        foreach (var holder in all)
        {
            foreach (var link in holder.Links.Where(link => holder == issue || link.Other == issue.Number))
            {
                var outward = holder == issue;
                var other = all.First(candidate => candidate.Number == (outward ? link.Other : holder.Number));
                var side = outward ? "outwardIssue" : "inwardIssue";

                links.Push(
                    new JsonObject
                    {
                        ["id"] = $"{40000 + holder.Number}{link.Other}",
                        ["type"] = new JsonObject
                        {
                            ["id"] = link.Type == "Blocks" ? "10000" : "10003",
                            ["name"] = link.Type,
                            ["inward"] = link.Inward,
                            ["outward"] = link.Outward,
                        },
                        [side] = Stub(other),
                    });
            }
        }

        return links;
    }
}
