using Geeratya.Core.Adf;
using Geeratya.Core.Panes;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;
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
    private const string DetailsTitle = "Details";
    private const int StatusGap = 2;
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

        AddTitle(sink, detail, columns, inner);

        if (detail.Issue is null && detail.IssueLoad is Failed failed)
        {
            var error = new PanelContent();

            error.Add(StyledLine.Of(FailureText(detail.Key, failed), TextRole.Error));
            error.Add(StyledLine.Of(BackText, TextRole.Muted));
            sink.AddPanel(PaneIds.Error, [new StyledSpan(ErrorTitle, TextRole.Error)], error, columns);

            return sink.ToDocument(columns);
        }

        AddDescription(sink, detail, columns, inner);
        IssueRelationsBuilder.Add(sink, detail, columns, inner);
        AddDetails(sink, detail, columns, inner);
        AddComments(sink, detail, columns, inner);

        return sink.ToDocument(columns);
    }

    private static string FailureText(string key, Failed failed)
    {
        return failed.Kind is JiraErrorKind.NotFound or JiraErrorKind.Forbidden ? NotFoundText(key) : failed.Message;
    }

    /// <summary>
    /// The title pane: the key and type in the top edge, the summary on the left of one row and the status in its
    /// category colour on the right. Two rows only when the summary does not fit beside the status.
    /// </summary>
    private static void AddTitle(DocumentSink sink, DetailState detail, int width, int inner)
    {
        var header = HeaderFields.Of(detail);
        var content = new PanelContent();
        var rows = SummaryRows(header, inner);

        if (header.Known && rows.Count > 0)
        {
            content.AddBlock(rows, new DetailTarget(DetailTargetKind.Field, DetailFieldIds.Summary));
        }
        else
        {
            content.AddRange(rows);
        }

        var title = new List<StyledSpan> { new(Clean(detail.Key), TextRole.Key) };

        if (header.TypeName.Length > 0)
        {
            title.Add(new StyledSpan($" · {Clean(header.TypeName)}", TextRole.Muted));
        }

        sink.AddPanel(PaneIds.Title, title, content, width);
    }

    private static List<StyledLine> SummaryRows(HeaderFields header, int inner)
    {
        var rows = new List<StyledLine>();

        if (header.Status is not { Name.Length: > 0 } current)
        {
            return Wrapped(header.Summary, inner);
        }

        var status = new StyledSpan(RelationTable.Cut(Clean(current.Name), inner), StatusRoles.Of(current));
        var statusWidth = DisplayWidth.Of(status.Text) + StatusGap;
        var wrapped = new WrappedText(Math.Max(1, inner - statusWidth), false);

        wrapped.Append(header.Summary, TextRole.Strong);
        wrapped.Finish(rows);

        if (rows.Count > 1)
        {
            rows = Wrapped(header.Summary, inner);
        }

        if (rows.Count == 0)
        {
            rows.Add(StyledLine.Empty);
        }

        var last = rows[^1];
        var room = inner - DisplayWidth.Of(last.PlainText) - DisplayWidth.Of(status.Text);

        if (room < StatusGap && last.Spans.Count > 0)
        {
            rows.Add(StyledLine.Empty);
            last = rows[^1];
            room = inner - DisplayWidth.Of(status.Text);
        }

        rows[^1] = new StyledLine([.. last.Spans, new StyledSpan(new string(' ', Math.Max(0, room)), TextRole.Body), status]);

        return rows;
    }

    private static List<StyledLine> Wrapped(string summary, int inner)
    {
        var rows = new List<StyledLine>();
        var wrapped = new WrappedText(inner, false);

        wrapped.Append(summary, TextRole.Strong);
        wrapped.Finish(rows);

        return rows;
    }

    /// <summary>
    /// The Details pane (until the sidebar of #704): the editable fields and the dates, one cursor stop per field.
    /// Nothing is drawn when the issue has no fields to show.
    /// </summary>
    private static void AddDetails(DocumentSink sink, DetailState detail, int width, int inner)
    {
        var fields = Fields(HeaderFields.Of(detail), detail.LoadedAt, detail.EstimateLabel);

        if (fields.Count == 0)
        {
            return;
        }

        var content = new PanelContent();

        foreach (var field in fields)
        {
            content.AddBlock(FieldRows(field, inner), new DetailTarget(DetailTargetKind.Field, field.Id));
        }

        sink.AddPanel(PaneIds.Details, [new StyledSpan(DetailsTitle, TextRole.Heading)], content, width);
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
        var expanded = detail.Expanded.Contains(DetailFieldIds.Description);
        var cut = target is not null && !expanded && content.Count > LongText.DescriptionRows;

        LongText.Add(panel, content, target, expanded, new LongTextLimit(LongText.DescriptionRows, inner, true));

        var title = new StyledSpan(DescriptionTitle, TextRole.Heading);

        if (!cut)
        {
            sink.AddPanel(PaneIds.Description, [title], panel, width);

            return;
        }

        var scroll = new PaneScroll(0, content.Count, LongText.DescriptionRows);

        sink.AddPanel(PaneIds.Description, [title], panel, width, [new StyledSpan(scroll.Chip(), TextRole.Muted)], scroll);
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

            AddComment(content, detail.Comments[index], detail, inner);
        }

        var footer = new List<StyledLine>();

        AddCommentsFooter(footer, detail);
        content.AddRange(footer);
        sink.AddPanel(PaneIds.Comments, [new StyledSpan(CommentsHeading(detail), TextRole.Heading)], content, width);
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
    private static void AddComment(PanelContent content, IssueComment comment, DetailState detail, int inner)
    {
        var reference = detail.LoadedAt;
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

        var lines = new List<StyledLine>();

        byline.Finish(lines);

        if (comment.Body.Content.Count == 0)
        {
            lines.Add(StyledLine.Of(EmptyCommentText, TextRole.Muted));
        }
        else
        {
            lines.AddRange(AdfRenderer.Render(comment.Body, inner).Lines);
        }

        var expanded = detail.Expanded.Contains(target.ExpandKey);

        LongText.Add(content, lines, target, expanded, new LongTextLimit(LongText.CommentLines, inner));
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

    private static string Clean(string text)
    {
        return TextClean.Of(text);
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
}
