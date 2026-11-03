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
    private const int PairingWidth = 60;
    private const int StackedFieldsWidth = 24;
    private const int ColumnGap = 3;
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
        var content = new List<StyledLine>();
        var wrapped = new WrappedText(inner, false);

        wrapped.Append(header.Summary, TextRole.Strong);
        wrapped.Finish(content);

        var fields = Fields(header, detail.LoadedAt);

        if (fields.Count > 0)
        {
            content.Add(StyledLine.Empty);
            AddFields(content, fields, inner);
        }

        var title = new List<StyledSpan> { new(Clean(detail.Key), TextRole.Key) };

        if (header.TypeName.Length > 0)
        {
            title.Add(new StyledSpan($" · {Clean(header.TypeName)}", TextRole.Muted));
        }

        sink.AddPanel(title, Wrap(content), width);
    }

    private static List<Field> Fields(HeaderFields header, DateTimeOffset? reference)
    {
        var fields = new List<Field>();

        if (header.Status is { } status)
        {
            fields.Add(new Field("Status", status.Name, StatusRoles.Of(status), true));
        }

        AddField(fields, "Priority", header.Priority, true);
        AddField(fields, "Assignee", header.Assignee, true);
        AddField(fields, "Reporter", header.Reporter);
        AddField(fields, "Labels", header.Labels.Count == 0 ? null : string.Join(", ", header.Labels), true);
        AddField(fields, "Parent", header.Parent);
        AddField(fields, "Epic", header.Epic);
        AddField(fields, "Sprint", header.Sprint);
        AddField(fields, "Estimate", header.Estimate);

        if (header.Updated is { } updated && reference is { } now)
        {
            AddField(fields, "Updated", RelativeTime.Format(updated, now));
        }

        return fields;
    }

    private static void AddField(List<Field> fields, string label, string? value, bool editable = false)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new Field(label, value, TextRole.Strong, editable));
        }
    }

    /// <summary>
    /// A label column of fixed width, then the value. On a wide panel two short fields share a row; a value that does
    /// not fit half the panel gets a row of its own and wraps under its first line.
    /// </summary>
    private static void AddFields(List<StyledLine> content, List<Field> fields, int inner)
    {
        var half = (inner - ColumnGap) / 2;
        var pairing = inner >= PairingWidth;
        Field? held = null;

        foreach (var field in fields)
        {
            if (pairing && field.Fits(half))
            {
                if (held is null)
                {
                    held = field;
                }
                else
                {
                    content.Add(FieldRow(held, half, field));
                    held = null;
                }

                continue;
            }

            if (held is not null)
            {
                content.Add(FieldRow(held, half, null));
                held = null;
            }

            AddFieldRows(content, field, inner);
        }

        if (held is not null)
        {
            content.Add(FieldRow(held, half, null));
        }
    }

    private static StyledLine FieldRow(Field left, int half, Field? right)
    {
        var spans = new List<StyledSpan>();

        left.AddCell(spans);

        if (right is not null)
        {
            spans.Add(new StyledSpan(new string(' ', Math.Max(0, half + ColumnGap - left.CellWidth)), TextRole.Body));
            right.AddCell(spans);
        }

        return new StyledLine(spans);
    }

    private static void AddFieldRows(List<StyledLine> content, Field field, int inner)
    {
        if (inner < StackedFieldsWidth)
        {
            var stacked = new WrappedText(inner, false);

            stacked.Append($"{field.Label}: ", TextRole.FieldLabel);
            stacked.Append(field.Value, field.Role);
            field.AppendMarker(stacked);
            stacked.Finish(content);

            return;
        }

        var wrapped = new WrappedText(Math.Max(1, inner - LabelWidth), false);
        var rows = new List<StyledLine>();

        wrapped.Append(field.Value, field.Role);
        field.AppendMarker(wrapped);
        wrapped.Finish(rows);

        for (var index = 0; index < rows.Count; index++)
        {
            var lead = index == 0
                ? new StyledSpan(field.PaddedLabel, TextRole.FieldLabel)
                : new StyledSpan(new string(' ', LabelWidth), TextRole.Body);

            content.Add(new StyledLine([lead, .. rows[index].Spans]));
        }
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

        sink.AddPanel([new StyledSpan(DescriptionTitle, TextRole.Heading)], Wrap(content), width);
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

    private static PanelContent Wrap(List<StyledLine> lines)
    {
        var content = new PanelContent();

        content.AddRange(lines);

        return content;
    }

    private static string CommentsHeading(DetailState detail)
    {
        var loaded = detail.Comments.Count;

        if (loaded == 0)
        {
            return CommentsTitle;
        }

        return detail.CommentTotal is { } total && total > loaded
            ? $"{CommentsTitle} ({loaded} of {total})"
            : $"{CommentsTitle} ({loaded})";
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

    private sealed record Field(string Label, string Value, TextRole Role, bool Editable = false)
    {
        public void AppendMarker(WrappedText text)
        {
            if (Editable)
            {
                text.Append(EditableMarker, TextRole.Editable);
            }
        }

        public string PaddedLabel => Label.PadRight(LabelWidth);

        /// <summary>Columns of the label and the value on one row.</summary>
        public int CellWidth => LabelWidth + DisplayWidth.Of(Clean(Value)) + (Editable ? DisplayWidth.Of(EditableMarker) : 0);

        public bool Fits(int half)
        {
            return CellWidth <= half;
        }

        public void AddCell(List<StyledSpan> spans)
        {
            spans.Add(new StyledSpan(PaddedLabel, TextRole.FieldLabel));
            spans.Add(new StyledSpan(Clean(Value), Role));

            if (Editable)
            {
                spans.Add(new StyledSpan(EditableMarker, TextRole.Editable));
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
        string? Estimate = null)
    {
        private static readonly HeaderFields _empty =
            new(string.Empty, string.Empty, null, null, null, null, [], null, null);

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
