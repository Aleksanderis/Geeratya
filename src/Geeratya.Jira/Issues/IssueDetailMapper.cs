using System.Text.Json;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search;

namespace Geeratya.Jira.Issues;

/// <summary>
/// Maps the wire shape of one issue to <see cref="IssueDetail"/>. Pure and tolerant: a missing or unknown field
/// becomes a neutral value, never an exception.
/// </summary>
internal static class IssueDetailMapper
{
    public static IssueDetail Map(IssueWire.Issue issue)
    {
        var fields = issue.Fields;
        var pool = new Dictionary<string, string>(StringComparer.Ordinal);

        return new IssueDetail(
            issue.Id ?? string.Empty,
            issue.Key ?? string.Empty,
            fields?.Summary ?? string.Empty,
            IssueSummaryMapper.MapStatus(fields?.Status, pool),
            IssueSummaryMapper.MapType(fields?.IssueType, pool),
            IssueSummaryMapper.Share(fields?.Priority?.Name, pool),
            IssueSummaryMapper.MapAssignee(fields?.Assignee, pool),
            IssueSummaryMapper.MapAssignee(fields?.Reporter, pool),
            fields?.Labels is { Count: > 0 } labels ? labels : [],
            IssueSummaryMapper.ParseTimestamp(fields?.Created),
            IssueSummaryMapper.ParseTimestamp(fields?.Updated),
            IssueSummaryMapper.Share(fields?.Project?.Key, pool) ?? string.Empty,
            MapDescription(fields?.Description),
            MapStub(fields?.Parent, pool),
            MapStubs(fields?.Subtasks, pool),
            MapLinks(fields?.IssueLinks, pool),
            MapAttachments(fields?.Attachments, pool));
    }

    private static AdfDocument? MapDescription(JsonElement? description)
    {
        if (description is not { } element || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var document = AdfParser.Parse(element);

        return document.Content.Count == 0 ? null : document;
    }

    private static IssueStub? MapStub(IssueWire.Stub? stub, Dictionary<string, string> pool)
    {
        if (stub?.Key is not { Length: > 0 } key)
        {
            return null;
        }

        var fields = stub.StubFields;

        return new IssueStub(
            key,
            fields?.Summary ?? string.Empty,
            IssueSummaryMapper.MapStatus(fields?.Status, pool),
            IssueSummaryMapper.MapType(fields?.IssueType, pool));
    }

    private static List<IssueStub> MapStubs(List<IssueWire.Stub>? wire, Dictionary<string, string> pool)
    {
        var result = new List<IssueStub>(wire?.Count ?? 0);

        foreach (var stub in wire ?? [])
        {
            if (MapStub(stub, pool) is { } mapped)
            {
                result.Add(mapped);
            }
        }

        return result;
    }

    private static List<IssueLink> MapLinks(List<IssueWire.Link>? wire, Dictionary<string, string> pool)
    {
        var result = new List<IssueLink>(wire?.Count ?? 0);

        foreach (var link in wire ?? [])
        {
            if (MapLink(link, pool) is { } mapped)
            {
                result.Add(mapped);
            }
        }

        return result;
    }

    /// <summary>
    /// Only the other issue is present: <c>outwardIssue</c> means "this issue &lt;outward text&gt; other",
    /// <c>inwardIssue</c> means "this issue &lt;inward text&gt; other". A link with neither is dropped.
    /// </summary>
    private static IssueLink? MapLink(IssueWire.Link? link, Dictionary<string, string> pool)
    {
        if (link is null)
        {
            return null;
        }

        var outward = MapStub(link.OutwardIssue, pool);

        if (outward is not null)
        {
            return new IssueLink(
                link.Id ?? string.Empty,
                link.Type?.Name ?? string.Empty,
                IssueLinkDirection.Outward,
                link.Type?.Outward ?? string.Empty,
                outward);
        }

        var inward = MapStub(link.InwardIssue, pool);

        if (inward is null)
        {
            return null;
        }

        return new IssueLink(
            link.Id ?? string.Empty,
            link.Type?.Name ?? string.Empty,
            IssueLinkDirection.Inward,
            link.Type?.Inward ?? string.Empty,
            inward);
    }

    private static List<IssueAttachment> MapAttachments(
        List<IssueWire.Attachment>? wire,
        Dictionary<string, string> pool)
    {
        var result = new List<IssueAttachment>(wire?.Count ?? 0);

        foreach (var attachment in wire ?? [])
        {
            if (attachment is null)
            {
                continue;
            }

            result.Add(new IssueAttachment(
                attachment.Id ?? string.Empty,
                attachment.Filename ?? string.Empty,
                IssueSummaryMapper.Share(attachment.MimeType, pool) ?? string.Empty,
                attachment.Size ?? 0,
                IssueSummaryMapper.ParseTimestamp(attachment.Created),
                IssueSummaryMapper.MapAssignee(attachment.Author, pool),
                attachment.Content));
        }

        return result;
    }
}
