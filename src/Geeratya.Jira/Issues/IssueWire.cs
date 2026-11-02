using System.Text.Json;
using System.Text.Json.Serialization;
using Geeratya.Jira.Search;

namespace Geeratya.Jira.Issues;

/// <summary>
/// The JSON shapes of <c>GET /issue/{key}</c> as Jira sends them. Every member is optional and unknown properties
/// are ignored; the description stays raw JSON for the ADF parser.
/// </summary>
internal static class IssueWire
{
    internal sealed record Issue(
        string? Id,
        string? Key,
        [property: JsonPropertyName(IssueFields.FieldsProperty)] Fields? Fields);

    /// <summary>An issue read with a caller-chosen field list: every field stays raw JSON, by id.</summary>
    internal sealed record FieldBag(
        [property: JsonPropertyName(IssueFields.FieldsProperty)] Dictionary<string, JsonElement>? Values);

    internal sealed record Fields(
        string? Summary,
        SearchWire.Status? Status,
        [property: JsonPropertyName(IssueFields.IssueType)] SearchWire.IssueType? IssueType,
        SearchWire.Priority? Priority,
        SearchWire.Assignee? Assignee,
        SearchWire.Assignee? Reporter,
        List<string>? Labels,
        string? Created,
        string? Updated,
        SearchWire.Project? Project,
        JsonElement? Description,
        Stub? Parent,
        List<Stub>? Subtasks,
        [property: JsonPropertyName(IssueFields.IssueLinks)] List<Link>? IssueLinks,
        [property: JsonPropertyName(IssueFields.Attachment)] List<Attachment>? Attachments);

    internal sealed record Stub(
        string? Key,
        [property: JsonPropertyName(IssueFields.FieldsProperty)] StubFields? StubFields);

    internal sealed record StubFields(
        string? Summary,
        SearchWire.Status? Status,
        [property: JsonPropertyName(IssueFields.IssueType)] SearchWire.IssueType? IssueType);

    internal sealed record Link(string? Id, LinkType? Type, Stub? InwardIssue, Stub? OutwardIssue);

    internal sealed record LinkType(string? Name, string? Inward, string? Outward);

    internal sealed record Attachment(
        string? Id,
        string? Filename,
        string? MimeType,
        long? Size,
        string? Created,
        SearchWire.Assignee? Author,
        string? Content);
}
