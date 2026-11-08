using System.Text.Json.Nodes;

namespace Geeratya.FakeJira;

public sealed partial class DemoJira
{
    private static readonly string[] _priorityNames = ["Highest", "High", "Medium", "Low", "Lowest"];

    private static FakeReply NoContent() => new(204, []);

    private static FakeReply BadRequest(string message)
    {
        return Json(new JsonObject { ["errorMessages"] = new JsonArray(message) }, 400);
    }

    private static FakeReply Transitions()
    {
        var transitions = new JsonArray();

        for (var index = 0; index < _statuses.Length; index++)
        {
            transitions.Push(
                new JsonObject
                {
                    ["id"] = $"{(index + 1) * 10 + 1}",
                    ["name"] = _statuses[index],
                    ["to"] = DemoJson.Status(_statuses[index]),
                    ["hasScreen"] = false,
                    ["isGlobal"] = true,
                    ["isInitial"] = false,
                    ["isAvailable"] = true,
                    ["isConditional"] = false,
                    ["fields"] = new JsonObject(),
                });
        }

        return Json(new JsonObject { ["expand"] = "transitions", ["transitions"] = transitions });
    }

    private static FakeReply Transition(DemoIssue issue, string body)
    {
        var id = JsonNode.Parse(body)?["transition"]?["id"]?.ToString();
        var index = int.TryParse(id, out var number) ? (number / 10) - 1 : -1;

        if (index < 0 || index >= _statuses.Length)
        {
            return BadRequest("Transition id is not valid for this issue.");
        }

        issue.Status = _statuses[index];
        issue.Updated = DateTimeOffset.UtcNow;

        return NoContent();
    }

    private static FakeReply Edit(DemoIssue issue, string body)
    {
        var request = JsonNode.Parse(body)?.AsObject() ?? [];

        if (request["fields"] is JsonObject fields)
        {
            foreach (var (name, value) in fields)
            {
                ApplyField(issue, name, value);
            }
        }

        if (request["update"]?["labels"] is JsonArray operations)
        {
            foreach (var operation in operations.OfType<JsonObject>())
            {
                ApplyLabel(issue, operation);
            }
        }

        issue.Updated = DateTimeOffset.UtcNow;

        return NoContent();
    }

    private static void ApplyField(DemoIssue issue, string name, JsonNode? value)
    {
        switch (name)
        {
            case "summary":
                issue.Summary = value?.ToString() ?? issue.Summary;

                break;
            case "priority":
                var priority = value?["name"]?.ToString() ?? _priorityNames.ElementAtOrDefault(
                    (int.TryParse(value?["id"]?.ToString(), out var id) ? id : 3) - 1);
                issue.Priority = priority ?? issue.Priority;

                break;
            case "labels":
                issue.Labels = [.. (value as JsonArray ?? []).Select(label => label!.ToString())];

                break;
            case "description":
                issue.Description = value?.DeepClone() ?? new JsonObject();

                break;
            case DemoJson.StoryPointsField:
                issue.Points = value is null ? null : (int)value.GetValue<double>();

                break;
        }
    }

    private static void ApplyLabel(DemoIssue issue, JsonObject operation)
    {
        if (operation["add"]?.ToString() is { } added && !issue.Labels.Contains(added))
        {
            issue.Labels.Add(added);
        }

        if (operation["remove"]?.ToString() is { } removed)
        {
            issue.Labels.Remove(removed);
        }
    }

    private static FakeReply Assign(DemoIssue issue, string body)
    {
        var account = JsonNode.Parse(body)?["accountId"]?.ToString();
        var index = Array.FindIndex(DemoDataset.People, person => person.AccountId == account);

        if (account is not null && index < 0)
        {
            return BadRequest("User does not exist.");
        }

        issue.Assignee = index;
        issue.Updated = DateTimeOffset.UtcNow;

        return NoContent();
    }

    private static FakeReply AddComment(DemoIssue issue, string body)
    {
        var text = AdfText(JsonNode.Parse(body)?["body"]);
        issue.Comments.Add(new DemoComment(0, 0, text));
        issue.Updated = DateTimeOffset.UtcNow;
        var created = DemoJson.Comment(issue.Comments[^1], CommentId(issue, issue.Comments.Count - 1));

        return Json(created, 201);
    }

    private static string AdfText(JsonNode? node)
    {
        if (node is null)
        {
            return string.Empty;
        }

        var text = node["text"]?.ToString() ?? string.Empty;
        var children = node["content"] as JsonArray ?? [];

        return $"{text}{string.Join(string.Empty, children.Select(AdfText))}";
    }

    private FakeReply MoveToSprint(int sprint, string body)
    {
        var keys = (JsonNode.Parse(body)?["issues"] as JsonArray ?? []).Select(key => key!.ToString()).ToList();
        var issues = keys.Select(key => _ranked.Find(issue => issue.Key == key)).ToList();

        if (issues.Count == 0 || issues.Any(issue => issue is null))
        {
            return BadRequest("Issue does not exist or you do not have permission to see it.");
        }

        if (sprint == 14)
        {
            return BadRequest("Cannot move issues into a closed sprint.");
        }

        foreach (var issue in issues)
        {
            issue!.Sprint = sprint;
        }

        return NoContent();
    }

    private FakeReply Rank(string body)
    {
        var request = JsonNode.Parse(body)?.AsObject() ?? [];
        var keys = (request["issues"] as JsonArray ?? []).Select(key => key!.ToString()).ToList();
        var moving = _ranked.Where(issue => keys.Contains(issue.Key)).ToList();
        var before = request["rankBeforeIssue"]?.ToString();
        var anchorKey = before ?? request["rankAfterIssue"]?.ToString();

        if (moving.Count == 0 || _ranked.Find(issue => issue.Key == anchorKey) is not { } anchor || moving.Contains(anchor))
        {
            return BadRequest("Cannot rank the issues.");
        }

        _ranked.RemoveAll(moving.Contains);
        _ranked.InsertRange(_ranked.IndexOf(anchor) + (before is null ? 1 : 0), moving);

        return NoContent();
    }
}
