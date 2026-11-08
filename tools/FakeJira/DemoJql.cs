using System.Text;
using System.Text.RegularExpressions;

namespace Geeratya.FakeJira;

/// <summary>
/// A small JQL evaluator over the demo dataset: and/or/parentheses, <c>= != in not in is is not ~ !~</c> on project,
/// assignee, reporter, status, statusCategory, resolution, priority, type, labels, sprint, parent, key and text fields,
/// and <c>order by</c>. A clause on a field it does not know (dates, for example) matches everything.
/// </summary>
public static partial class DemoJql
{
    private const string OrderBy = "order by";
    private const string CurrentUser = "currentuser()";

    private static readonly string[] _priorities = ["Highest", "High", "Medium", "Low", "Lowest"];
    private static readonly string[] _statuses = ["To Do", "In Progress", "In Review", "Done"];

    [GeneratedRegex(
        @"^\s*""?([A-Za-z][\w ]*?)""?\s*(!=|!~|>=|<=|=|~|>|<|\bnot\s+in\b|\bin\b|\bis\s+not\b|\bis\b)\s*(.+?)\s*$",
        RegexOptions.IgnoreCase)]
    private static partial Regex Clause();

    /// <summary>The issues matching <paramref name="jql"/>, in its order (the rank order when it names none).</summary>
    public static List<DemoIssue> Run(string? jql, IReadOnlyList<DemoIssue> ranked)
    {
        var text = jql ?? string.Empty;
        var orderAt = text.IndexOf(OrderBy, StringComparison.OrdinalIgnoreCase);
        var filter = orderAt >= 0 ? text[..orderAt] : text;
        var matches = ranked.Where(issue => Matches(filter, issue)).ToList();

        return orderAt >= 0 ? Sort(matches, ranked, text[(orderAt + OrderBy.Length)..]) : matches;
    }

    /// <summary>The sprint state of a sprint number: 14 is closed, 15 active, 16 future.</summary>
    public static string SprintState(int sprint) => sprint switch { 14 => "closed", 15 => "active", _ => "future" };

    public static string CategoryKey(string status) => status switch { "Done" => "done", "To Do" => "new", _ => "indeterminate" };

    private static bool Matches(string expression, DemoIssue issue)
    {
        if (expression.Trim().Length == 0)
        {
            return true;
        }

        var alternatives = SplitTop(expression, "or");

        if (alternatives.Count > 1)
        {
            return alternatives.Any(part => Matches(part, issue));
        }

        var conditions = SplitTop(expression, "and");

        if (conditions.Count > 1)
        {
            return conditions.All(part => Matches(part, issue));
        }

        var single = expression.Trim();

        if (single.StartsWith('(') && single.EndsWith(')') && SplitTop(single[1..^1], "or").Count >= 1
            && Balanced(single))
        {
            return Matches(single[1..^1], issue);
        }

        return Clause().Match(single) is { Success: true } match && Evaluate(match, issue);
    }

    private static int Nesting(char character)
    {
        return character switch { '(' => 1, ')' => -1, _ => 0 };
    }

    private static string CategoryName(string status)
    {
        return CategoryKey(status) switch { "done" => "Done", "new" => "To Do", _ => "In Progress" };
    }

    private static bool Balanced(string text)
    {
        var depth = 0;
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            quoted ^= text[index] == '"';

            if (quoted)
            {
                continue;
            }

            depth += Nesting(text[index]);

            if (depth == 0 && index < text.Length - 1)
            {
                return false;
            }
        }

        return depth == 0;
    }

    private static List<string> SplitTop(string text, string word)
    {
        var parts = new List<string>();
        var depth = 0;
        var quoted = false;
        var start = 0;
        var separator = $" {word} ";

        for (var index = 0; index < text.Length; index++)
        {
            quoted ^= text[index] == '"';

            if (quoted)
            {
                continue;
            }

            depth += Nesting(text[index]);

            if (depth == 0 && string.Compare(text, index, separator, 0, separator.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                parts.Add(text[start..index]);
                start = index + separator.Length;
                index = start - 1;
            }
        }

        parts.Add(text[start..]);

        return parts;
    }

    private static bool Evaluate(Match match, DemoIssue issue)
    {
        var field = match.Groups[1].Value.Trim().ToLowerInvariant().Replace(" ", string.Empty);
        var operation = Regex.Replace(match.Groups[2].Value.ToLowerInvariant(), @"\s+", " ");
        var wanted = Values(match.Groups[3].Value);

        if (operation is "~" or "!~")
        {
            var found = Contains(field, issue, wanted);

            return operation == "~" ? found : !found;
        }

        if (Actual(field, issue) is not { } actual)
        {
            return true;
        }

        var hit = actual.Any(item => wanted.Any(value => Equal(item, value)));
        var emptyWanted = wanted.All(value => value.Equals("empty", StringComparison.OrdinalIgnoreCase)
            || value.Equals("null", StringComparison.OrdinalIgnoreCase));

        return operation switch
        {
            "=" or "in" => emptyWanted ? actual.Count == 0 : hit,
            "!=" or "not in" => emptyWanted ? actual.Count > 0 : !hit,
            "is" => actual.Count == 0,
            "is not" => actual.Count > 0,
            _ => true,
        };
    }

    private static bool Equal(string item, string value)
    {
        if (value.Equals(CurrentUser, StringComparison.OrdinalIgnoreCase))
        {
            return item == DemoDataset.People[0].AccountId || item == DemoDataset.People[0].Name;
        }

        return item.Equals(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool Contains(string field, DemoIssue issue, List<string> wanted)
    {
        var description = DemoAdf.PlainText(issue.Description);
        var comments = string.Join(' ', issue.Comments.Select(comment => comment.Text));
        var haystack = field switch
        {
            "summary" => issue.Summary,
            "description" => description,
            "text" => $"{issue.Summary} {description} {comments}",
            _ => null,
        };

        // As for the other clauses, a field the evaluator does not know matches everything.
        return haystack is null || wanted.All(
            value => value.Replace("*", string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .All(word => haystack.Contains(word, StringComparison.OrdinalIgnoreCase)));
    }

    private static List<string>? Actual(string field, DemoIssue issue)
    {
        return field switch
        {
            "project" => [DemoDataset.ProjectKey, "Wayfinder"],
            "assignee" => PersonValues(issue.Assignee),
            "reporter" => [DemoDataset.People[issue.Reporter].AccountId, DemoDataset.People[issue.Reporter].Name],
            "status" => [issue.Status],
            "statuscategory" => [CategoryName(issue.Status), CategoryKey(issue.Status)],
            "resolution" => issue.Status == "Done" ? ["Done"] : ["Unresolved"],
            "priority" => [issue.Priority],
            "type" or "issuetype" => [issue.Type],
            "labels" => [.. issue.Labels],
            "key" or "issuekey" => [issue.Key],
            "parent" or "epiclink" or "epic" => issue.Epic == 0 ? [] : [$"{DemoDataset.ProjectKey}-{issue.Epic}"],
            "sprint" => SprintValues(issue),
            _ => null,
        };
    }

    private static List<string> PersonValues(int person)
    {
        return person < 0 ? [] : [DemoDataset.People[person].AccountId, DemoDataset.People[person].Name];
    }

    private static List<string> SprintValues(DemoIssue issue)
    {
        if (issue.Sprint == 0)
        {
            return [];
        }

        var state = SprintState(issue.Sprint);
        var values = new List<string> { issue.Sprint.ToString(), $"Sprint {issue.Sprint}" };

        if (state == "closed")
        {
            values.Add("closedSprints()");
        }
        else
        {
            // As in the generated fake, openSprints() is the active sprint only; a planned one is futureSprints().
            values.Add(state == "active" ? "openSprints()" : "futureSprints()");
        }

        return values;
    }

    private static List<string> Values(string text)
    {
        var inner = text.Trim();

        if (inner.StartsWith('(') && inner.EndsWith(')'))
        {
            inner = inner[1..^1];
        }

        var values = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var depth = 0;

        foreach (var character in inner)
        {
            quoted ^= character == '"';

            if (!quoted)
            {
                depth += Nesting(character);
            }

            if (character == ',' && !quoted && depth == 0)
            {
                values.Add(current.ToString().Trim().Trim('"'));
                current.Clear();

                continue;
            }

            current.Append(character);
        }

        values.Add(current.ToString().Trim().Trim('"'));

        return values;
    }

    private static List<DemoIssue> Sort(List<DemoIssue> matches, IReadOnlyList<DemoIssue> ranked, string order)
    {
        IEnumerable<DemoIssue> result = matches;

        foreach (var key in order.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Reverse())
        {
            var words = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var descending = words.Length > 1 && words[1].Equals("desc", StringComparison.OrdinalIgnoreCase);
            var name = words[0].Trim('"').ToLowerInvariant();
            var ordered = result.ToList();
            var keys = ordered.ToDictionary(issue => issue, issue => SortKey(name, issue, ranked));

            result = descending ? ordered.OrderByDescending(issue => keys[issue]) : ordered.OrderBy(issue => keys[issue]);
        }

        return [.. result];
    }

    private static string SortKey(string name, DemoIssue issue, IReadOnlyList<DemoIssue> ranked)
    {
        return name switch
        {
            "priority" => Pad(_priorities.Length - Array.IndexOf(_priorities, issue.Priority)), // As in Jira, DESC is Highest first.
            "status" => Pad(Array.IndexOf(_statuses, issue.Status)),
            "key" or "issuekey" => Pad(issue.Number),
            "created" => Pad((int)(100000 - (issue.CreatedDaysAgo * 100))),
            "updated" => issue.Updated.UtcTicks.ToString("D20"),
            "summary" => issue.Summary,
            "assignee" => issue.Assignee < 0 ? "~" : DemoDataset.People[issue.Assignee].Name,
            _ => Pad(ranked.ToList().IndexOf(issue)),
        };
    }

    private static string Pad(int number) => number.ToString("D8");
}
