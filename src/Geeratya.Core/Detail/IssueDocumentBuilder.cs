using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Detail;

/// <summary>
/// Lays an opened issue out as styled lines for a width: header, description, comments (<c>docs/ux-screens.md</c>,
/// "Issue detail"). Pure: the same state and width give the same lines, and relative times are taken against
/// <see cref="DetailState.LoadedAt"/> rather than a clock. Only the screen's window of the lines is drawn, so the
/// result is built once per change of data or width, not per frame.
/// </summary>
public static class IssueDocumentBuilder
{
    private const int StackedFieldsWidth = 24;
    private const int LabelWidth = 10;
    private const string EditableMarker = " ▾";
    private const string DescriptionTitle = "Description";
    private const string CommentsTitle = "Comments";
    private const string ErrorTitle = "Error";
    private const string LoadingText = "Loading…";
    private const string NoDescriptionText = "No description.";
    private const string NoCommentsText = "No comments.";
    private const string EmptyCommentText = "(no text)";
    private const string LoadMoreText = "Load more comments (Enter)";
    private const string LoadingMoreText = "Loading more comments…";
    private const string RetryText = " (Enter to retry)";
    private const string BackText = "Esc to go back.";
    private const string UnknownAuthor = "Unknown";
    private const string EditedText = " (edited)";
    private const string TooManyText = "Showing the first comments only.";

    private static readonly TimeSpan _editedAfter = TimeSpan.FromMinutes(1);

    /// <summary>The not-found text, shared with the status and the tests.</summary>
    public static string NotFoundText(string key)
    {
        return $"Not found or no access: {key}";
    }

    public static DetailDocument Build(DetailState detail, int width)
    {
        var columns = Math.Max(1, width);
        var inner = Panel.InnerWidth(columns);
        var sink = new DocumentSink();

        AddHeader(sink, detail, columns, inner);

        if (detail.Issue is null && detail.IssueLoad is Failed failed)
        {
            var error = new PanelContent();

            error.Add(StyledLine.Of(FailureText(detail.Key, failed), TextRole.Error));
            error.Add(StyledLine.Of(BackText, TextRole.Muted));
            sink.AddPanel([new StyledSpan(ErrorTitle, TextRole.Error)], error, columns);

            return sink.ToDocument(columns);
        }

        AddDescription(sink, detail, columns, inner);
        IssueRelationsBuilder.Add(sink, detail, columns, inner);
        AddComments(sink, detail, columns, inner);

        return sink.ToDocument(columns);
    }

    private static string FailureText(string key, Failed failed)
    {
        return failed.Kind is JiraErrorKind.NotFound or JiraErrorKind.Forbidden ? NotFoundText(key) : failed.Message;
    }

    private static void AddHeader(DocumentSink sink, DetailState detail, int width, int inner)
    {
        var header = HeaderFields.Of(detail);
        var content = new PanelContent();
        var wrapped = new WrappedText(inner, false);
        var summary = new List<StyledLine>();

        wrapped.Append(header.Summary, TextRole.Strong);
        wrapped.Finish(summary);

        if (header.Known && summary.Count > 0)
        {
            content.AddBlock(summary, new DetailTarget(DetailTargetKind.Field, DetailFieldIds.Summary));
        }
        else
        {
            content.AddRange(summary);
        }

        var fields = Fields(header, detail.LoadedAt, detail.EstimateLabel);

        if (fields.Count > 0)
        {
            content.AddGap(StyledLine.Empty);

            foreach (var field in fields)
            {
                content.AddBlock(FieldRows(field, inner), new DetailTarget(DetailTargetKind.Field, field.Id));
            }
        }

        var title = new List<StyledSpan> { new(Clean(detail.Key), TextRole.Key) };

        if (header.TypeName.Length > 0)
        {
            title.Add(new StyledSpan($" · {Clean(header.TypeName)}", TextRole.Muted));
        }

        IReadOnlyList<StyledSpan>? status = header.Status is { Name.Length: > 0 } current
            ? [new StyledSpan(Clean(current.Name), StatusRoles.Of(current))]
            : null;

        sink.AddPanel(title, content, width, status);
    }

    private static List<Field> Fields(HeaderFields header, DateTimeOffset? reference, string? estimateLabel)
    {
        var fields = new List<Field>();

        if (header.Status is { } status)
        {
            fields.Add(new Field(DetailFieldIds.Status, "Status", status.Name, StatusRoles.Of(status), true));
        }

        if (header.Known)
        {
            AddOrPlaceholder(fields, DetailFieldIds.Priority, "Priority", header.Priority, "None");
            AddOrPlaceholder(fields, DetailFieldIds.Assignee, "Assignee", header.Assignee, "Unassigned");
        }

        AddField(fields, DetailFieldIds.Reporter, "Reporter", header.Reporter);

        if (header.Known)
        {
            var labels = header.Labels.Count == 0 ? null : string.Join(", ", header.Labels);

            AddOrPlaceholder(fields, DetailFieldIds.Labels, "Labels", labels, "none");
        }

        AddField(fields, DetailFieldIds.Parent, "Parent", header.Parent);
        AddField(fields, DetailFieldIds.Epic, "Epic", header.Epic);
        AddField(fields, DetailFieldIds.Sprint, "Sprint", header.Sprint, true);

        var estimate = string.IsNullOrWhiteSpace(estimateLabel) ? "Estimate" : Clean(estimateLabel);

        AddField(fields, DetailFieldIds.Estimate, estimate, header.Estimate, true);

        if (header.Updated is { } updated && reference is { } now)
        {
            AddField(fields, DetailFieldIds.Updated, "Updated", RelativeTime.Format(updated, now));
        }

        return fields;
    }

    private static void AddField(List<Field> fields, string id, string label, string? value, bool editable = false)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new Field(id, label, value, TextRole.Strong, editable));
        }
    }

    /// <summary>An editable field that always shows, so it can be set: a missing value reads as muted text.</summary>
    private static void AddOrPlaceholder(List<Field> fields, string id, string label, string? value, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new Field(id, label, placeholder, TextRole.Muted, true));
        }
        else
        {
            fields.Add(new Field(id, label, value, TextRole.Strong, true));
        }
    }

    /// <summary>
    /// A label column of fixed width, then the value, one field per cursor stop: a value that does not fit wraps under
    /// its first line. Below 24 inner columns the label and value run together.
    /// </summary>
    private static List<StyledLine> FieldRows(Field field, int inner)
    {
        var content = new List<StyledLine>();

        if (inner < StackedFieldsWidth)
        {
            var stacked = new WrappedText(inner, false);

            stacked.Append($"{field.Label}: ", TextRole.FieldLabel);
            stacked.Append(Clean(field.Value), field.Role);
            field.AppendMarker(stacked);
            stacked.Finish(content);

            return content;
        }

        var wrapped = new WrappedText(Math.Max(1, inner - LabelWidth), false);
        var rows = new List<StyledLine>();

        wrapped.Append(Clean(field.Value), field.Role);
        field.AppendMarker(wrapped);
        wrapped.Finish(rows);

        for (var index = 0; index < rows.Count; index++)
        {
            var lead = index == 0
                ? new StyledSpan(field.PaddedLabel, TextRole.FieldLabel)
                : new StyledSpan(new string(' ', LabelWidth), TextRole.Body);

            content.Add(new StyledLine([lead, .. rows[index].Spans]));
        }

        return content;
    }

    private static void AddDescription(DocumentSink sink, DetailState detail, int width, int inner)
    {
        var content = new List<StyledLine>();

        if (detail.Issue is null)
        {
            content.Add(StyledLine.Of(LoadingText, TextRole.Loading));
        }
        else if (detail.Issue.Description is null)
        {
            content.Add(StyledLine.Of(NoDescriptionText, TextRole.Muted));
        }
        else
        {
            content.AddRange(AdfRenderer.Render(detail.Issue.Description, inner).Lines);
        }

        var panel = new PanelContent();
        var target = detail.Issue is null ? null : new DetailTarget(DetailTargetKind.Field, DetailFieldIds.Description);

        foreach (var line in content)
        {
            panel.Add(line, target);
        }

        sink.AddPanel([new StyledSpan(DescriptionTitle, TextRole.Heading)], panel, width);
    }

    private static void AddComments(DocumentSink sink, DetailState detail, int width, int inner)
    {
        var content = new PanelContent();

        for (var index = 0; index < detail.Comments.Count; index++)
        {
            if (index > 0)
            {
                content.Add(StyledLine.Empty);
            }

            AddComment(content, detail.Comments[index], detail.LoadedAt, inner);
        }

        var footer = new List<StyledLine>();

        AddCommentsFooter(footer, detail);
        content.AddRange(footer);
        sink.AddPanel([new StyledSpan(CommentsHeading(detail), TextRole.Heading)], content, width);
    }

    private static string CommentsHeading(DetailState detail)
    {
        var loaded = detail.Comments.Count;

        if (loaded == 0)
        {
            return CommentsTitle;
        }

        return detail.CommentTotal is { } total && total > loaded
            ? $"{CommentsTitle} {loaded} of {total}"
            : $"{CommentsTitle} {loaded}";
    }

    /// <summary>One comment; every line of it opens the comment, so the edit and delete keys find it.</summary>
    private static void AddComment(PanelContent content, IssueComment comment, DateTimeOffset? reference, int inner)
    {
        var target = new DetailTarget(DetailTargetKind.Comment, comment.Id);
        var byline = new WrappedText(inner, false);

        byline.Append(Clean(comment.Author?.DisplayName ?? UnknownAuthor), TextRole.Strong);

        if (comment.Created is { } created && reference is { } now)
        {
            byline.Append($" · {RelativeTime.Format(created, now)}", TextRole.Muted);
        }

        if (comment.Created is { } first && comment.Updated is { } last && last - first > _editedAfter)
        {
            byline.Append(EditedText, TextRole.Muted);
        }

        content.Finish(byline, target);

        if (comment.Body.Content.Count == 0)
        {
            content.Add(StyledLine.Of(EmptyCommentText, TextRole.Muted), target);

            return;
        }

        foreach (var line in AdfRenderer.Render(comment.Body, inner).Lines)
        {
            content.Add(line, target);
        }
    }

    private static void AddCommentsFooter(List<StyledLine> lines, DetailState detail)
    {
        switch (detail.CommentsLoad)
        {
            case Loading when detail.Comments.Count == 0:
                lines.Add(StyledLine.Of(LoadingText, TextRole.Loading));
                break;
            case Loading:
                lines.Add(StyledLine.Of(LoadingMoreText, TextRole.Loading));
                break;
            case Failed failed:
                lines.Add(StyledLine.Of($"{failed.Message}{RetryText}", TextRole.Error));
                break;
            default:
                AddIdleFooter(lines, detail);
                break;
        }
    }

    private static void AddIdleFooter(List<StyledLine> lines, DetailState detail)
    {
        if (detail.NextComments is not null)
        {
            lines.Add(StyledLine.Of(LoadMoreText, TextRole.Link));
        }
        else if (detail.Comments.Count == 0)
        {
            lines.Add(StyledLine.Of(NoCommentsText, TextRole.Muted));
        }
        else if (detail.CommentTotal is { } total && total > detail.Comments.Count)
        {
            lines.Add(StyledLine.Of(TooManyText, TextRole.Muted));
        }
    }

    /// <summary>Replaces control characters, which a terminal would act on, with spaces.</summary>
    private static string Clean(string text)
    {
        if (!text.Any(char.IsControl))
        {
            return text;
        }

        return string.Concat(text.Select(static character => char.IsControl(character) ? ' ' : character));
    }

    private sealed record Field(string Id, string Label, string Value, TextRole Role, bool Editable = false)
    {
        public string PaddedLabel => Label.PadRight(LabelWidth);

        public void AppendMarker(WrappedText text)
        {
            if (Editable)
            {
                text.Append(EditableMarker, TextRole.Editable);
            }
        }
    }

    private sealed record HeaderFields(
        string TypeName,
        string Summary,
        IssueStatus? Status,
        string? Priority,
        string? Assignee,
        string? Reporter,
        IReadOnlyList<string> Labels,
        string? Parent,
        DateTimeOffset? Updated,
        string? Epic = null,
        string? Sprint = null,
        string? Estimate = null,
        bool Known = true)
    {
        private static readonly HeaderFields _empty =
            new(string.Empty, string.Empty, null, null, null, null, [], null, null, Known: false);

        public static HeaderFields Of(DetailState detail)
        {
            if (detail.Issue is { } issue)
            {
                return FromIssue(issue);
            }

            return detail.Row is { } row ? FromRow(row) : _empty;
        }

        private static HeaderFields FromIssue(IssueDetail issue)
        {
            var parent = issue.Parent is { } stub ? $"{stub.Key} {stub.Summary}".TrimEnd() : null;
            var isEpic = issue.Epic is not null;

            return new HeaderFields(
                issue.IssueType.Name,
                issue.Summary,
                issue.Status,
                issue.Priority,
                issue.Assignee?.DisplayName,
                issue.Reporter?.DisplayName,
                issue.Labels,
                isEpic ? null : parent,
                issue.Updated,
                isEpic ? parent : null,
                SprintText(issue.Sprints),
                issue.Estimate is { } estimate ? EstimateText.Format(estimate) : null);
        }

        private static HeaderFields FromRow(IssueSummary row)
        {
            var parent = row.Parent is { } stub ? $"{stub.Key} {stub.Summary}".TrimEnd() : null;

            return new HeaderFields(
                row.IssueType.Name,
                row.Summary,
                row.Status,
                row.Priority,
                row.Assignee?.DisplayName,
                null,
                row.Labels,
                row.Epic is null ? parent : null,
                row.Updated,
                row.Epic is null ? null : parent,
                SprintText(row.Sprints),
                row.Estimate is { } estimate ? EstimateText.Format(estimate) : null);
        }

        /// <summary>The current sprint with its state, then the earlier sprints the issue was carried through.</summary>
        private static string? SprintText(IReadOnlyList<IssueSprint> sprints)
        {
            if (IssueSprint.Current(sprints) is not { } current)
            {
                return null;
            }

            var text = $"{current.Name} ({current.State.ToString().ToLowerInvariant()})";
            var earlier = sprints
                .Where(sprint => sprint.Id != current.Id && sprint.State == SprintState.Closed)
                .Select(sprint => sprint.Name)
                .ToList();

            return earlier.Count == 0 ? text : $"{text}; earlier: {string.Join(", ", earlier)}";
        }
    }
}
