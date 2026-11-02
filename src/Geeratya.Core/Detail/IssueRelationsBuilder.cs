using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The relation panels of the issue detail (<c>docs/ux-screens.md</c>, "Issue detail"): sub-tasks, epic children,
/// linked issues, remote links and attachments, in that order. A panel is drawn only when it has entries, is loading
/// or has failed; a failed one shows its error and leaves the others alone. Attachments and remote links are text:
/// name, type and size, or title and URL, never the file.
/// </summary>
internal static class IssueRelationsBuilder
{
    private const string SubtasksTitle = "Sub-tasks";
    private const string ChildrenTitle = "Epic children";
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
            AddStubs(lines, SubtasksTitle, issue.Subtasks, width, inner);
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

    private static void AddStubs(
        DocumentSink lines,
        string title,
        IReadOnlyList<IssueStub> stubs,
        int width,
        int inner)
    {
        if (stubs.Count == 0)
        {
            return;
        }

        var content = new PanelContent();

        foreach (var stub in stubs)
        {
            var text = new WrappedText(inner, false);

            AddIssue(text, stub.Key, stub.Summary, stub.Status);
            content.Finish(text, IssueTarget(stub.Key));
        }

        lines.AddPanel(Title(title, stubs.Count), content, width);
    }

    private static void AddChildren(DocumentSink lines, DetailState detail, int width, int inner)
    {
        var content = new PanelContent();

        switch (detail.ChildrenLoad)
        {
            case Loading:
                content.Add(StyledLine.Of(LoadingText, TextRole.Loading));
                lines.AddPanel([new StyledSpan(ChildrenTitle, TextRole.Heading)], content, width);

                return;
            case Failed failed:
                content.Add(StyledLine.Of(failed.Message, TextRole.Error));
                lines.AddPanel([new StyledSpan(ChildrenTitle, TextRole.Heading)], content, width);

                return;
        }

        if (detail.Children is not { Count: > 0 } children)
        {
            return;
        }

        foreach (var child in children)
        {
            var text = new WrappedText(inner, false);

            AddIssue(text, child.Key, child.Summary, child.Status);
            text.Append($"{Separator}{child.IssueType.Name}", TextRole.Muted);
            content.Finish(text, IssueTarget(child.Key));
        }

        if (detail.ChildrenMore)
        {
            content.Add(StyledLine.Of(FirstChildrenOnlyText, TextRole.Muted));
        }

        var count = detail.ChildrenMore ? $"{children.Count}+" : children.Count.ToString();

        lines.AddPanel([new StyledSpan($"{ChildrenTitle} ({count})", TextRole.Heading)], content, width);
    }

    private static void AddLinks(DocumentSink lines, IReadOnlyList<IssueLink> links, int width, int inner)
    {
        if (links.Count == 0)
        {
            return;
        }

        var content = new PanelContent();

        foreach (var link in links)
        {
            var text = new WrappedText(inner, false);

            text.Append(DirectionText(link), TextRole.Emphasis);
            text.Append(" ", TextRole.Body);
            AddIssue(text, link.Issue.Key, link.Issue.Summary, link.Issue.Status);
            content.Finish(text, LinkTarget(link));
        }

        lines.AddPanel(Title(LinksTitle, links.Count), content, width);
    }

    private static void AddRemoteLinks(DocumentSink lines, DetailState detail, int width, int inner)
    {
        var content = new PanelContent();

        switch (detail.LinksLoad)
        {
            case Loading:
                content.Add(StyledLine.Of(LoadingText, TextRole.Loading));
                lines.AddPanel([new StyledSpan(RemoteLinksTitle, TextRole.Heading)], content, width);

                return;
            case Failed failed:
                content.Add(StyledLine.Of(failed.Message, TextRole.Error));
                lines.AddPanel([new StyledSpan(RemoteLinksTitle, TextRole.Heading)], content, width);

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

        lines.AddPanel(Title(RemoteLinksTitle, links.Count), content, width);
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

            content.Finish(text, new DetailTarget(DetailTargetKind.Attachment, key));
        }

        lines.AddPanel(Title(AttachmentsTitle, attachments.Count), content, width);
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

    private static void AddIssue(WrappedText text, string key, string summary, IssueStatus status)
    {
        text.Append(key, TextRole.Key);

        if (summary.Length > 0)
        {
            text.Append($" {summary}", TextRole.Body);
        }

        if (status.Name.Length > 0)
        {
            text.Append($"{Separator}{status.Name}", StatusRoles.Of(status));
        }
    }

    /// <summary>The link type's wording for this side, or the type name when Jira sent none.</summary>
    private static string DirectionText(IssueLink link)
    {
        return string.IsNullOrWhiteSpace(link.DirectionText) ? link.TypeName : link.DirectionText;
    }

    private static StyledSpan[] Title(string name, int count)
    {
        return [new StyledSpan($"{name} ({count})", TextRole.Heading)];
    }
}
