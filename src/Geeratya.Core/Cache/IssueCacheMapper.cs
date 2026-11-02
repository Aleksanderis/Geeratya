using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Cache;

/// <summary>Converts between <see cref="IssueDetail"/> and the cached issue file DTOs. Pure.</summary>
public static class IssueCacheMapper
{
    /// <summary>The issue file format version.</summary>
    public const int Schema = 1;

    public static CachedIssueFile ToFile(CachedIssue issue)
    {
        var detail = issue.Detail;

        return new CachedIssueFile(
            Schema,
            issue.SavedAt,
            detail.Id,
            detail.Key,
            detail.Summary,
            ToStatus(detail.Status),
            ToType(detail.IssueType),
            detail.Priority,
            ToPerson(detail.Assignee),
            ToPerson(detail.Reporter),
            [.. detail.Labels],
            detail.Created,
            detail.Updated,
            detail.ProjectKey,
            detail.Description is null ? null : AdfCacheMapper.ToDto(detail.Description),
            detail.Parent is null ? null : ToStub(detail.Parent),
            [.. detail.Subtasks.Select(ToStub)],
            [.. detail.Links.Select(ToLink)],
            [.. detail.Attachments.Select(ToAttachment)]);
    }

    /// <summary>The issue in <paramref name="file"/>; null when the schema or content is not readable.</summary>
    public static CachedIssue? FromFile(CachedIssueFile file)
    {
        if (file.Schema != Schema || !IsComplete(file))
        {
            return null;
        }

        var links = new List<IssueLink>();

        foreach (var link in file.Links)
        {
            if (!Enum.TryParse(link.Direction, out IssueLinkDirection direction) || !Enum.IsDefined(direction))
            {
                return null;
            }

            links.Add(new IssueLink(link.Id, link.TypeName, direction, link.DirectionText, FromStub(link.Issue)));
        }

        var detail = new IssueDetail(
            file.Id,
            file.Key,
            file.Summary,
            FromStatus(file.Status),
            FromType(file.IssueType),
            file.Priority,
            FromPerson(file.Assignee),
            FromPerson(file.Reporter),
            [.. file.Labels],
            file.Created,
            file.Updated,
            file.ProjectKey,
            file.Description is null ? null : AdfCacheMapper.FromDto(file.Description),
            file.Parent is null ? null : FromStub(file.Parent),
            [.. file.Subtasks.Select(FromStub)],
            links,
            [.. file.Attachments.Select(FromAttachment)]);

        return new CachedIssue(detail, file.SavedAt);
    }

    private static CachedStatus ToStatus(IssueStatus status)
    {
        return new CachedStatus(status.Id, status.Name, status.CategoryKey);
    }

    private static IssueStatus FromStatus(CachedStatus status)
    {
        return new IssueStatus(status.Id, status.Name, status.CategoryKey);
    }

    private static CachedIssueType ToType(IssueTypeSummary type)
    {
        return new CachedIssueType(type.Id, type.Name, type.HierarchyLevel, type.IsSubtask);
    }

    private static IssueTypeSummary FromType(CachedIssueType type)
    {
        return new IssueTypeSummary(type.Id, type.Name, type.HierarchyLevel, type.IsSubtask);
    }

    private static CachedPerson? ToPerson(IssueAssignee? person)
    {
        return person is null ? null : new CachedPerson(person.AccountId, person.DisplayName);
    }

    private static IssueAssignee? FromPerson(CachedPerson? person)
    {
        return person is null ? null : new IssueAssignee(person.AccountId, person.DisplayName);
    }

    private static CachedStub ToStub(IssueStub stub)
    {
        return new CachedStub(stub.Key, stub.Summary, ToStatus(stub.Status), ToType(stub.IssueType));
    }

    private static IssueStub FromStub(CachedStub stub)
    {
        return new IssueStub(stub.Key, stub.Summary, FromStatus(stub.Status), FromType(stub.IssueType));
    }

    private static CachedLink ToLink(IssueLink link)
    {
        return new CachedLink(
            link.Id,
            link.TypeName,
            link.Direction.ToString(),
            link.DirectionText,
            ToStub(link.Issue));
    }

    private static CachedAttachment ToAttachment(IssueAttachment attachment)
    {
        return new CachedAttachment(
            attachment.Id,
            attachment.FileName,
            attachment.MimeType,
            attachment.Size,
            attachment.Created,
            ToPerson(attachment.Author),
            attachment.ContentUrl);
    }

    private static IssueAttachment FromAttachment(CachedAttachment attachment)
    {
        return new IssueAttachment(
            attachment.Id,
            attachment.FileName,
            attachment.MimeType,
            attachment.Size,
            attachment.Created,
            FromPerson(attachment.Author),
            attachment.ContentUrl);
    }

    // JSON can hold nulls where the DTOs promise values, so these checks are not redundant.
    // ReSharper disable ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
    /// <summary>A file from disk can hold JSON nulls where the DTOs promise values; such a file is unusable.</summary>
    private static bool IsComplete(CachedIssueFile file)
    {
        return file is { Status: not null, IssueType: not null, Labels: not null, Subtasks: not null }
            && file is { Links: not null, Attachments: not null }
            && file.Id is not null
            && file.Key is not null
            && file.Summary is not null
            && file.ProjectKey is not null
            && file.Subtasks.All(IsComplete)
            && file.Links.All(IsComplete)
            && file.Attachments.All(IsComplete)
            && (file.Parent is null || IsComplete(file.Parent))
            && (file.Description is null || file.Description.Content is not null);
    }

    private static bool IsComplete(CachedLink? link)
    {
        return link is { Id: not null, TypeName: not null, DirectionText: not null, Direction: not null }
            && IsComplete(link.Issue);
    }

    private static bool IsComplete(CachedStub? stub)
    {
        return stub is { Key: not null, Summary: not null, Status: not null, IssueType: not null };
    }

    private static bool IsComplete(CachedAttachment? attachment)
    {
        return attachment is { Id: not null, FileName: not null, MimeType: not null };
    }

    // ReSharper restore ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
}
