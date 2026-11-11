using System.Globalization;
using System.Text.Json;
using Geeratya.Jira.Paging;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints;

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

    private static readonly IReadOnlyList<IssueSprint> _noSprints = [];
    private static readonly IReadOnlyList<IssueSubtask> _noSubtasks = [];

    private const string IdProperty = "id";
    private const string NameProperty = "name";
    private const string StateProperty = "state";
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffzzz";
    private const int OffsetWithoutColonLength = 5;

    public static IssuePage MapPage(SearchWire.Response response, IssueExtraFields? extras = null)
    {
        var items = MapIssues(response.Issues, extras);
        var next = HasNext(response) ? new PageCursor(response.NextPageToken!) : null;

        return new IssuePage(items, next, null);
    }

    /// <summary>Maps a list of wire issues; null gives an empty list.</summary>
    public static List<IssueSummary> MapIssues(List<SearchWire.Issue>? wire, IssueExtraFields? extras = null)
    {
        wire ??= [];
        var items = new List<IssueSummary>(wire.Count);
        var pool = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < wire.Count; index++)
        {
            items.Add(MapIssue(wire[index], pool, extras));
        }

        return items;
    }

    private static bool HasNext(SearchWire.Response response) =>
        !response.IsLast && !string.IsNullOrEmpty(response.NextPageToken);

    private static IssueSummary MapIssue(
        SearchWire.Issue issue,
        Dictionary<string, string> pool,
        IssueExtraFields? extras)
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
            ParseTimestamp(fields?.Updated),
            Share(fields?.Project?.Key, pool) ?? string.Empty)
        {
            Sprints = MapSprints(fields?.Extra, extras?.SprintFieldId),
            Estimate = MapEstimate(fields?.Extra, extras?.EstimateFieldIds),
            Subtasks = extras is { IncludeSubtasks: true } ? MapSubtasks(fields?.Extra, pool) : _noSubtasks,
        };
    }

    /// <summary>
    /// The sprints of the Sprint field of an issue read whole (detail): an array of objects, <c>null</c> for none. Entries without an id are skipped
    /// and a field of another shape gives an empty list.
    /// </summary>
    internal static IReadOnlyList<IssueSprint> MapSprints(Dictionary<string, JsonElement>? extra, string? fieldId)
    {
        if (fieldId is null || extra is null || !extra.TryGetValue(fieldId, out var field)
            || field.ValueKind != JsonValueKind.Array)
        {
            return _noSprints;
        }

        var result = new List<IssueSprint>();

        foreach (var entry in field.EnumerateArray())
        {
            if (entry.ValueKind == JsonValueKind.Object && entry.TryGetProperty(IdProperty, out var id)
                && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var sprintId))
            {
                var state = SprintMapper.ParseState(TextOf(entry, StateProperty));

                result.Add(new IssueSprint(sprintId, TextOf(entry, NameProperty), state));
            }
        }

        return result.Count == 0 ? _noSprints : result;
    }

    /// <summary>
    /// The sprints of a search row's Sprint field: entries without an id were dropped while reading and a field of
    /// another shape gives an empty list.
    /// </summary>
    internal static IReadOnlyList<IssueSprint> MapSprints(Dictionary<string, ExtraValue>? extra, string? fieldId)
    {
        if (fieldId is null || extra is null || !extra.TryGetValue(fieldId, out var field)
            || field.Sprints is not { } wire)
        {
            return _noSprints;
        }

        var result = new List<IssueSprint>(wire.Count);

        foreach (var entry in wire)
        {
            result.Add(new IssueSprint(entry.Id, entry.Name ?? string.Empty, SprintMapper.ParseState(entry.State)));
        }

        return result;
    }

    /// <summary>
    /// The sub-task stubs of the <c>subtasks</c> field (entries without a key were dropped while reading); a missing
    /// or odd field gives an empty list.
    /// </summary>
    internal static List<IssueSubtask> MapSubtasks(
        Dictionary<string, ExtraValue>? extra,
        Dictionary<string, string> pool)
    {
        var result = new List<IssueSubtask>();

        if (extra is null || !extra.TryGetValue(IssueFields.Subtasks, out var field) || field.Subtasks is not { } wire)
        {
            return result;
        }

        foreach (var entry in wire)
        {
            result.Add(new IssueSubtask(
                entry.Key,
                entry.Fields?.Summary ?? string.Empty,
                MapStatus(entry.Fields?.Status, pool),
                entry.Fields?.IssueType?.Name ?? string.Empty));
        }

        return result;
    }

    /// <summary>The first candidate estimate field of a search row that holds a number; null when none does.</summary>
    internal static double? MapEstimate(Dictionary<string, ExtraValue>? extra, IReadOnlyList<string>? fieldIds)
    {
        if (extra is null || fieldIds is null)
        {
            return null;
        }

        foreach (var fieldId in fieldIds)
        {
            if (extra.TryGetValue(fieldId, out var field) && field.Number is { } value)
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>The first candidate estimate field of an issue read whole (detail) that holds a number.</summary>
    internal static double? MapEstimate(Dictionary<string, JsonElement>? extra, IReadOnlyList<string>? fieldIds)
    {
        if (extra is null || fieldIds is null)
        {
            return null;
        }

        foreach (var fieldId in fieldIds)
        {
            if (extra.TryGetValue(fieldId, out var field) && field.ValueKind == JsonValueKind.Number
                && field.TryGetDouble(out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static string TextOf(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    internal static IssueStatus MapStatus(SearchWire.Status? status, Dictionary<string, string> pool) =>
        new(
            status?.Id ?? string.Empty,
            Share(status?.Name, pool) ?? string.Empty,
            Share(status?.StatusCategory?.Key, pool) ?? UndefinedCategoryKey);

    internal static IssueTypeSummary MapType(SearchWire.IssueType? type, Dictionary<string, string> pool) =>
        new(
            type?.Id ?? string.Empty,
            Share(type?.Name, pool) ?? string.Empty,
            type?.HierarchyLevel ?? 0,
            type?.Subtask ?? false);

    internal static IssueAssignee? MapAssignee(SearchWire.Assignee? assignee, Dictionary<string, string> pool)
    {
        if (assignee is null)
        {
            return null;
        }

        return new IssueAssignee(
            Share(assignee.AccountId, pool) ?? string.Empty,
            Share(assignee.DisplayName, pool) ?? string.Empty);
    }

    private static IssueParent? MapParent(SearchWire.Parent? parent)
    {
        if (parent?.Key is not { Length: > 0 } key)
        {
            return null;
        }

        var isEpic = parent.Stub?.IssueType?.HierarchyLevel == IssueDetail.EpicHierarchyLevel;

        return new IssueParent(key, parent.Stub?.Summary, isEpic);
    }

    internal static string? Share(string? value, Dictionary<string, string> pool)
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
    internal static DateTimeOffset? ParseTimestamp(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var normalized = WithOffsetColon(text);

        var isParsed = DateTimeOffset.TryParseExact(
            normalized,
            TimestampFormat,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed);

        if (isParsed)
        {
            return parsed;
        }

        return null;
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
