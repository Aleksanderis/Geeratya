using System.Globalization;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Search;

/// <summary>
/// Maps the wire shape of a search page to <see cref="IssuePage"/>. Pure and tolerant: a missing or unknown field
/// becomes a neutral value, never an exception. Repeated names (status, type, priority, project, assignee) are
/// shared within a page so a 10k-issue result does not hold 10k copies.
/// </summary>
internal static class IssueSummaryMapper
{
    /// <summary>Jira's own category key for a status without a category.</summary>
    public const string UndefinedCategoryKey = "undefined";

    private const string UpdatedFormat = "yyyy-MM-dd'T'HH:mm:ss.fffzzz";
    private const int OffsetWithoutColonLength = 5;

    public static IssuePage MapPage(SearchWire.Response response)
    {
        var wire = response.Issues ?? [];
        var items = new List<IssueSummary>(wire.Count);
        var pool = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < wire.Count; index++)
        {
            items.Add(MapIssue(wire[index], pool));
        }

        var next = HasNext(response) ? new PageCursor(response.NextPageToken!) : null;

        return new IssuePage(items, next, null);
    }

    private static bool HasNext(SearchWire.Response response) =>
        !response.IsLast && !string.IsNullOrEmpty(response.NextPageToken);

    private static IssueSummary MapIssue(SearchWire.Issue issue, Dictionary<string, string> pool)
    {
        var fields = issue.Fields;

        return new IssueSummary(
            issue.Id ?? string.Empty,
            issue.Key ?? string.Empty,
            fields?.Summary ?? string.Empty,
            MapStatus(fields?.Status, pool),
            MapType(fields?.IssueType, pool),
            Share(fields?.Priority?.Name, pool),
            MapAssignee(fields?.Assignee, pool),
            fields?.Labels is { Count: > 0 } labels ? labels : [],
            MapParent(fields?.Parent),
            ParseUpdated(fields?.Updated),
            Share(fields?.Project?.Key, pool) ?? string.Empty);
    }

    private static IssueStatus MapStatus(SearchWire.Status? status, Dictionary<string, string> pool) =>
        new(
            status?.Id ?? string.Empty,
            Share(status?.Name, pool) ?? string.Empty,
            Share(status?.StatusCategory?.Key, pool) ?? UndefinedCategoryKey);

    private static IssueTypeSummary MapType(SearchWire.IssueType? type, Dictionary<string, string> pool) =>
        new(
            type?.Id ?? string.Empty,
            Share(type?.Name, pool) ?? string.Empty,
            type?.HierarchyLevel ?? 0,
            type?.Subtask ?? false);

    private static IssueAssignee? MapAssignee(SearchWire.Assignee? assignee, Dictionary<string, string> pool)
    {
        if (assignee is null)
        {
            return null;
        }

        return new IssueAssignee(
            Share(assignee.AccountId, pool) ?? string.Empty,
            Share(assignee.DisplayName, pool) ?? string.Empty);
    }

    private static IssueParent? MapParent(SearchWire.Parent? parent) =>
        parent?.Key is { Length: > 0 } key ? new IssueParent(key, parent.Fields?.Summary) : null;

    private static string? Share(string? value, Dictionary<string, string> pool)
    {
        if (value is null)
        {
            return null;
        }

        if (pool.TryGetValue(value, out var shared))
        {
            return shared;
        }

        pool[value] = value;

        return value;
    }

    /// <summary>Jira writes the offset without a colon (<c>+0200</c>); an unreadable value gives null.</summary>
    private static DateTimeOffset? ParseUpdated(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var normalized = WithOffsetColon(text);

        return DateTimeOffset.TryParseExact(
            normalized,
            UpdatedFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
    }

    private static string WithOffsetColon(string text)
    {
        if (text.Length <= OffsetWithoutColonLength || text[^3] == ':')
        {
            return text;
        }

        var sign = text[^OffsetWithoutColonLength];

        return sign is '+' or '-' ? $"{text[..^2]}:{text[^2..]}" : text;
    }
}
