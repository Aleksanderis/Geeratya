using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;
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
    private const int FieldsSideBySideWidth = 80;
    private const int Indent = 2;
    private const string FieldGap = "   ";
    private const string Divider = "─";
    private const string DescriptionTitle = "Description";
    private const string CommentsTitle = "Comments";
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
        var lines = new List<StyledLine>();

        AddHeader(lines, detail, columns);

        if (detail.Issue is null && detail.IssueLoad is Failed failed)
        {
            lines.Add(StyledLine.Empty);
            lines.Add(StyledLine.Of(FailureText(detail.Key, failed), TextRole.Error));
            lines.Add(StyledLine.Of(BackText, TextRole.Muted));

            return new DetailDocument(columns, lines);
        }

        AddDescription(lines, detail, columns);
        AddComments(lines, detail, columns);

        return new DetailDocument(columns, lines);
    }

    private static string FailureText(string key, Failed failed)
    {
        return failed.Kind is JiraErrorKind.NotFound or JiraErrorKind.Forbidden ? NotFoundText(key) : failed.Message;
    }

    private static void AddHeader(List<StyledLine> lines, DetailState detail, int width)
    {
        var header = HeaderFields.Of(detail);

        lines.Add(new StyledLine(
        [
            new StyledSpan(Clean(detail.Key), TextRole.Key),
            new StyledSpan($"  {Clean(header.TypeName)}", TextRole.Muted),
        ]));

        AddWrapped(lines, header.Summary, TextRole.Strong, width);
        AddFields(lines, Fields(header, detail.LoadedAt), width);
        lines.Add(StyledLine.Of(string.Concat(Enumerable.Repeat(Divider, width)), TextRole.Muted));
    }

    private static List<Field> Fields(HeaderFields header, DateTimeOffset? reference)
    {
        var fields = new List<Field>();

        if (header.Status is { } status)
        {
            fields.Add(new Field("Status", status.Name, StatusRole(status)));
        }

        AddField(fields, "Priority", header.Priority);
        AddField(fields, "Assignee", header.Assignee);
        AddField(fields, "Reporter", header.Reporter);
        AddField(fields, "Labels", header.Labels.Count == 0 ? null : string.Join(", ", header.Labels));
        AddField(fields, "Parent", header.Parent);

        if (header.Updated is { } updated && reference is { } now)
        {
            AddField(fields, "Updated", RelativeTime.Format(updated, now));
        }

        return fields;
    }

    private static void AddField(List<Field> fields, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields.Add(new Field(label, value, TextRole.Body));
        }
    }

    /// <summary>
    /// Side by side while they fit on a wide terminal, one per line below 80 columns or when one alone is too long
    /// (that one wraps).
    /// </summary>
    private static void AddFields(List<StyledLine> lines, List<Field> fields, int width)
    {
        var spans = new List<StyledSpan>();
        var used = 0;

        foreach (var field in fields)
        {
            var size = field.Width;
            var alone = width < FieldsSideBySideWidth || size > width;

            if (spans.Count > 0 && (alone || used + FieldGap.Length + size > width))
            {
                lines.Add(new StyledLine(spans.ToArray()));
                spans.Clear();
                used = 0;
            }

            if (size > width)
            {
                AddWrappedField(lines, field, width);

                continue;
            }

            if (spans.Count > 0)
            {
                spans.Add(new StyledSpan(FieldGap, TextRole.Body));
                used += FieldGap.Length;
            }

            spans.Add(new StyledSpan($"{field.Label}: ", TextRole.Muted));
            spans.Add(new StyledSpan(Clean(field.Value), field.Role));
            used += size;
        }

        if (spans.Count > 0)
        {
            lines.Add(new StyledLine(spans.ToArray()));
        }
    }

    private static void AddWrappedField(List<StyledLine> lines, Field field, int width)
    {
        var wrapped = new WrappedText(width, false);

        wrapped.Append($"{field.Label}: ", TextRole.Muted);
        wrapped.Append(field.Value, field.Role);
        wrapped.Finish(lines);
    }

    private static void AddWrapped(List<StyledLine> lines, string text, TextRole role, int width)
    {
        var wrapped = new WrappedText(width, false);

        wrapped.Append(text, role);
        wrapped.Finish(lines);
    }

    private static void AddDescription(List<StyledLine> lines, DetailState detail, int width)
    {
        lines.Add(StyledLine.Of(DescriptionTitle, TextRole.Heading));

        if (detail.Issue is null)
        {
            lines.Add(StyledLine.Of($"{new string(' ', Indent)}{LoadingText}", TextRole.Loading));

            return;
        }

        if (detail.Issue.Description is null)
        {
            lines.Add(StyledLine.Of($"{new string(' ', Indent)}{NoDescriptionText}", TextRole.Muted));

            return;
        }

        AddIndented(lines, detail.Issue.Description, width);
    }

    private static void AddComments(List<StyledLine> lines, DetailState detail, int width)
    {
        lines.Add(StyledLine.Empty);
        lines.Add(StyledLine.Of(CommentsHeading(detail), TextRole.Heading));

        foreach (var comment in detail.Comments)
        {
            AddComment(lines, comment, detail.LoadedAt, width);
        }

        AddCommentsFooter(lines, detail);
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

    private static void AddComment(List<StyledLine> lines, IssueComment comment, DateTimeOffset? reference, int width)
    {
        lines.Add(StyledLine.Empty);

        var spans = new List<StyledSpan> { new(Clean(comment.Author?.DisplayName ?? UnknownAuthor), TextRole.Strong) };

        if (comment.Created is { } created && reference is { } now)
        {
            spans.Add(new StyledSpan($" · {RelativeTime.Format(created, now)}", TextRole.Muted));
        }

        if (comment.Created is { } first && comment.Updated is { } last && last - first > _editedAfter)
        {
            spans.Add(new StyledSpan(EditedText, TextRole.Muted));
        }

        lines.Add(new StyledLine(spans));

        if (comment.Body.Content.Count == 0)
        {
            lines.Add(StyledLine.Of($"{new string(' ', Indent)}{EmptyCommentText}", TextRole.Muted));

            return;
        }

        AddIndented(lines, comment.Body, width);
    }

    private static void AddCommentsFooter(List<StyledLine> lines, DetailState detail)
    {
        var indent = new string(' ', Indent);

        switch (detail.CommentsLoad)
        {
            case Loading when detail.Comments.Count == 0:
                lines.Add(StyledLine.Of($"{indent}{LoadingText}", TextRole.Loading));
                break;
            case Loading:
                lines.Add(StyledLine.Of($"{indent}{LoadingMoreText}", TextRole.Loading));
                break;
            case Failed failed:
                lines.Add(StyledLine.Of($"{indent}{failed.Message}{RetryText}", TextRole.Error));
                break;
            default:
                AddIdleFooter(lines, detail, indent);
                break;
        }
    }

    private static void AddIdleFooter(List<StyledLine> lines, DetailState detail, string indent)
    {
        if (detail.NextComments is not null)
        {
            lines.Add(StyledLine.Of($"{indent}{LoadMoreText}", TextRole.Link));
        }
        else if (detail.Comments.Count == 0)
        {
            lines.Add(StyledLine.Of($"{indent}{NoCommentsText}", TextRole.Muted));
        }
        else if (detail.CommentTotal is { } total && total > detail.Comments.Count)
        {
            lines.Add(StyledLine.Of($"{indent}{TooManyText}", TextRole.Muted));
        }
    }

    private static void AddIndented(List<StyledLine> lines, AdfDocument document, int width)
    {
        var rendering = AdfRenderer.Render(document, Math.Max(1, width - Indent));
        var indent = new StyledSpan(new string(' ', Indent), TextRole.Body);

        foreach (var line in rendering.Lines)
        {
            lines.Add(line.Spans.Count == 0 ? line : new StyledLine([indent, .. line.Spans]));
        }
    }

    private static TextRole StatusRole(IssueStatus status)
    {
        return status.CategoryKey switch
        {
            StatusCategoryKeys.New => TextRole.StatusToDo,
            StatusCategoryKeys.Indeterminate => TextRole.StatusInProgress,
            StatusCategoryKeys.Done => TextRole.StatusDone,
            _ => TextRole.Body,
        };
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

    private sealed record Field(string Label, string Value, TextRole Role)
    {
        public int Width => DisplayWidth.Of(Label) + 2 + DisplayWidth.Of(Clean(Value));
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
        DateTimeOffset? Updated)
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
            var parent = issue.Parent is { } stub ? $"{stub.Key} {stub.Summary}" : null;

            return new HeaderFields(
                issue.IssueType.Name,
                issue.Summary,
                issue.Status,
                issue.Priority,
                issue.Assignee?.DisplayName,
                issue.Reporter?.DisplayName,
                issue.Labels,
                parent,
                issue.Updated);
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
                parent,
                row.Updated);
        }
    }
}
