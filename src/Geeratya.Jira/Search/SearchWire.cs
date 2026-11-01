using System.Text.Json.Serialization;

namespace Geeratya.Jira.Search;

/// <summary>
/// The JSON shapes of <c>/search/jql</c> as Jira sends them. Every member is optional: Jira omits absent fields
/// (a missing <c>parent</c> is absent, an unassigned <c>assignee</c> is <c>null</c>) and the mapper never throws.
/// </summary>
internal static class SearchWire
{
    internal sealed record Response(List<Issue>? Issues, string? NextPageToken, bool IsLast);

    internal sealed record Issue(string? Id, string? Key, Fields? Fields);

    internal sealed record Fields(
        string? Summary,
        Status? Status,
        [property: JsonPropertyName(IssueFields.IssueType)] IssueType? IssueType,
        Priority? Priority,
        Assignee? Assignee,
        List<string>? Labels,
        Parent? Parent,
        string? Updated,
        Project? Project);

    internal sealed record Status(string? Id, string? Name, StatusCategory? StatusCategory);

    internal sealed record StatusCategory(string? Key);

    internal sealed record IssueType(
        string? Id,
        string? Name,
        int? HierarchyLevel,
        [property: JsonPropertyName("subtask")] bool? Subtask);

    internal sealed record Priority(string? Name);

    internal sealed record Assignee(string? AccountId, string? DisplayName);

    internal sealed record Parent(string? Key, ParentFields? Fields);

    internal sealed record ParentFields(string? Summary);

    internal sealed record Project(string? Key);

    internal sealed record Body(string Jql, int MaxResults, IReadOnlyList<string> Fields, string? NextPageToken);

    internal sealed record CountBody(string Jql);

    internal sealed record CountResponse(int Count);
}
