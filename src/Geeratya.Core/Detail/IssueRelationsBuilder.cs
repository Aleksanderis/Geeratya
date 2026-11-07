using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The relation panes of the issue detail (<c>docs/internal/ux-screens.md</c>, "Pane layout for the issue detail"): sub-tasks,
/// child issues, linked issues, remote links and attachments, in that order. The first three share one table
/// (<see cref="RelationTable"/>). A pane is drawn only when it has entries, is loading or has failed; a failed one
/// shows its error and leaves the others alone. Attachments and remote links are text:
/// name, type and size, or title and URL, never the file.
/// </summary>
internal static class IssueRelationsBuilder
{
    private const string SubtasksTitle = "Sub-tasks";
    private const string ChildrenTitle = "Child issues";
    private const string LinksTitle = "Linked issues";
    private const string RemoteLinksTitle = "Remote links";
    private const string AttachmentsTitle = "Attachments";
    private const string LoadingText = "Loading…";
    private const string Separator = " · ";
    private const string UnknownType = "unknown type";
    private const string FirstChildrenOnlyText = "Showing the first children only.";

    public static void Add(DocumentSink lines, DetailState detail, int width, int inner)
    {
        var issue = detail.Issue;

        if (issue is not null)
        {
            AddSubtasks(lines, detail, issue, width, inner);
        }

        AddChildren(lines, detail, width, inner);

        if (issue is not null)
        {
            AddLinks(lines, issue.Links, width, inner);
        }

        AddRemoteLinks(lines, detail, width, inner);

        if (issue is not null)
        {
            AddAttachments(lines, detail.Key, issue.Attachments, detail.LoadedAt, width, inner);
        }
    }

    /// <summary>
    /// The sub-tasks as a table. Jira lists them without an assignee, so the names come from the children search
    /// (<c>parent = KEY</c>) that loads beside the issue; a row stays blank there until it arrives.
    /// </summary>
    private static void AddSubtasks(DocumentSink lines, DetailState detail, IssueDetail issue, int width, int inner)
    {
        var stubs = issue.Subtasks;

        if (stubs.Count == 0)
        {
            return;
        }

        var assignees = new Dictionary<string, string?>();

        foreach (var child in detail.Children ?? [])
        {
            assignees.TryAdd(child.Key, child.Assignee?.DisplayName);
        }

        var rows = stubs
            .Select(stub => StubRow(stub, assignees.TryGetValue(stub.Key, out var name), name))
            .ToList();
        var content = new PanelContent();
        var done = stubs.Count(stub => stub.Status.CategoryKey == StatusCategoryKeys.Done);

        RelationTable.Add(content, rows, inner);
        lines.AddPanel(
            PaneIds.Subtasks,
            Title(SubtasksTitle, stubs.Count),
            content,
            width,
            [new StyledSpan($"{done} of {stubs.Count} done", TextRole.Muted)]);
    }

    private static void AddChildren(DocumentSink lines, DetailState detail, int width, int inner)
    {
        if (detail.Issue is not { IssueType.HierarchyLevel: >= DetailPaging.ParentLevel })
        {
            return;
        }

        var content = new PanelContent();
        var heading = new StyledSpan(ChildrenTitle, TextRole.Heading);

        switch (detail.ChildrenLoad)
        {
            case Loading:
                content.Add(StyledLine.Of(LoadingText, TextRole.Loading));
                lines.AddPanel(PaneIds.Children, [heading], content, width);

                return;
            case Failed failed:
                content.Add(StyledLine.Of(failed.Message, TextRole.Error));
                lines.AddPanel(PaneIds.Children, [heading], content, width);

                return;
        }

        if (detail.Children is not { Count: > 0 } children)
        {
            return;
        }

        var rows = children
            .Select(child => new RelationRow(
                child.Key,
                child.Summary,
                child.Status.Name,
                StatusRoles.Of(child.Status),
                IssueTarget(child.Key),
                child.Assignee?.DisplayName,
                true))
            .ToList();

        RelationTable.Add(content, rows, inner);

        if (detail.ChildrenMore)
        {
            content.Add(StyledLine.Of(FirstChildrenOnlyText, TextRole.Muted));
        }

        var count = detail.ChildrenMore ? $"{children.Count}+" : children.Count.ToString();

        var title = new StyledSpan($"{ChildrenTitle} {count}", TextRole.Heading);

        lines.AddPanel(PaneIds.Children, [title], content, width);
    }

    private static void AddLinks(DocumentSink lines, IReadOnlyList<IssueLink> links, int width, int inner)
    {
        if (links.Count == 0)
        {
            return;
        }

        var content = new PanelContent();
        var rows = links
            .Select(link => StubRow(link.Issue, false, null) with
            {
                Prefix = DirectionText(link),
                Target = LinkTarget(link),
            })
            .ToList();

        RelationTable.Add(content, rows, inner);
        lines.AddPanel(PaneIds.Links, Title(LinksTitle, links.Count), content, width);
    }

    private static RelationRow StubRow(IssueStub stub, bool assigneeKnown, string? assignee)
    {
        return new RelationRow(
            stub.Key,
            stub.Summary,
            stub.Status.Name,
            StatusRoles.Of(stub.Status),
            IssueTarget(stub.Key),
            assignee,
            assigneeKnown);
    }

    private static void AddRemoteLinks(DocumentSink lines, DetailState detail, int width, int inner)
    {
        var content = new PanelContent();
        var heading = new StyledSpan(RemoteLinksTitle, TextRole.Heading);

        switch (detail.LinksLoad)
        {
            case Loading:
                content.Add(StyledLine.Of(LoadingText, TextRole.Loading));
                lines.AddPanel(PaneIds.RemoteLinks, [heading], content, width);

                return;
            case Failed failed:
                content.Add(StyledLine.Of(failed.Message, TextRole.Error));
                lines.AddPanel(PaneIds.RemoteLinks, [heading], content, width);

                return;
        }

        if (detail.RemoteLinks is not { Count: > 0 } links)
        {
            return;
        }

        foreach (var link in links)
        {
            AddRemoteLink(content, link, inner);
        }

        lines.AddPanel(PaneIds.RemoteLinks, Title(RemoteLinksTitle, links.Count), content, width);
    }

    private static void AddRemoteLink(PanelContent content, RemoteLink link, int inner)
    {
        var title = new WrappedText(inner, false);

        title.Append(link.Title, TextRole.Strong);

        if (!string.IsNullOrWhiteSpace(link.ApplicationName))
        {
            title.Append($"{Separator}{link.ApplicationName}", TextRole.Muted);
        }

        var target = new DetailTarget(DetailTargetKind.RemoteLink, link.Url);

        content.Finish(title, target);

        var address = new WrappedText(inner, true);

        address.Append(link.Url, TextRole.Link);
        content.Finish(address, target);
    }

    private static void AddAttachments(
        DocumentSink lines,
        string key,
        IReadOnlyList<IssueAttachment> attachments,
        DateTimeOffset? reference,
        int width,
        int inner)
    {
        if (attachments.Count == 0)
        {
            return;
        }

        var content = new PanelContent();

        foreach (var attachment in attachments)
        {
            var text = new WrappedText(inner, false);
            var type = string.IsNullOrWhiteSpace(attachment.MimeType) ? UnknownType : attachment.MimeType;

            text.Append(attachment.FileName, TextRole.Strong);
            text.Append($"{Separator}{type}{Separator}{FileSize.Format(attachment.Size)}", TextRole.Muted);

            if (attachment.Created is { } created && reference is { } now)
            {
                text.Append($"{Separator}{RelativeTime.Format(created, now)}", TextRole.Muted);
            }

            content.Finish(text, new DetailTarget(DetailTargetKind.Attachment, key, AttachmentId: attachment.Id));
        }

        lines.AddPanel(PaneIds.Attachments, Title(AttachmentsTitle, attachments.Count), content, width);
    }

    /// <summary>The row opens the issue only for a well-formed key; anything else stays plain text.</summary>
    private static DetailTarget? IssueTarget(string key)
    {
        return IssueUrls.IsValidKey(key) ? new DetailTarget(DetailTargetKind.Issue, key) : null;
    }

    /// <summary>The row of a link opens the other issue and carries the link id, so the link can be removed.</summary>
    private static DetailTarget? LinkTarget(IssueLink link)
    {
        var target = IssueTarget(link.Issue.Key);

        return target is null ? null : target with { LinkId = link.Id };
    }

    /// <summary>The link type's wording for this side, or the type name when Jira sent none.</summary>
    private static string DirectionText(IssueLink link)
    {
        return string.IsNullOrWhiteSpace(link.DirectionText) ? link.TypeName : link.DirectionText;
    }

    private static StyledSpan[] Title(string name, int count)
    {
        return [new StyledSpan($"{name} {count}", TextRole.Heading)];
    }
}
