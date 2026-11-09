using Geeratya.Core.Adf;
using Geeratya.Core.Text;
using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// The content rows of the Comments pane (<c>docs/internal/ux-screens.md</c>, "Comments"): per comment a header bar (author
/// left, time and <c>edited</c> right), the body cut after <see cref="BodyLines"/> lines with a note row, and one
/// empty row; then the footer (load more, loading, error). Pure. The cursor rests on the header bar of a cut comment
/// and on every body line of an expanded one.
/// </summary>
public static class CommentRows
{
    /// <summary>Body lines shown before the rest is cut (6 when the pane is zoomed, #705).</summary>
    public const int BodyLines = 3;

    private const string LoadingText = "Loading…";
    private const string NoCommentsText = "No comments.";
    private const string EmptyCommentText = "(no text)";
    private const string LoadMoreText = "Load more comments (Enter)";
    private const string LoadingMoreText = "Loading more comments…";
    private const string RetryText = " (Enter to retry)";
    private const string UnknownAuthor = "Unknown";
    private const string EditedText = "edited";
    private const string TooManyNewestText = "Showing the newest comments only.";
    private const string TooManyOldestText = "Showing the oldest comments only (<space>mo shows the newest).";
    private const int BarGap = 2;

    private static readonly TimeSpan _editedAfter = TimeSpan.FromMinutes(1);

    public static IReadOnlyList<CommentRow> Build(DetailState detail, int inner, int bodyLines = BodyLines)
    {
        var rows = new List<CommentRow>();

        for (var index = 0; index < detail.Comments.Count; index++)
        {
            AddComment(rows, detail.Comments[index], index, detail, inner, bodyLines);
        }

        AddFooter(rows, detail);

        return rows;
    }

    /// <summary>The index of the row where the comment <paramref name="id"/> starts, -1 when it is not shown.</summary>
    public static int HeaderOf(IReadOnlyList<CommentRow> rows, string id)
    {
        for (var index = 0; index < rows.Count; index++)
        {
            if (rows[index].Target is { Kind: DetailTargetKind.Comment } target && target.Value == id)
            {
                return index;
            }
        }

        return -1;
    }

    /// <summary>The last row of the comment that holds <paramref name="row"/>, the gap excluded.</summary>
    public static int EndOf(IReadOnlyList<CommentRow> rows, int row)
    {
        var comment = rows[row].Comment;
        var end = row;

        while (end + 1 < rows.Count && rows[end + 1].Comment == comment && rows[end + 1].Target is not null)
        {
            end++;
        }

        return end;
    }

    private static void AddComment(
        List<CommentRow> rows,
        IssueComment comment,
        int index,
        DetailState detail,
        int inner,
        int bodyLines)
    {
        var expanded = detail.Expanded.Contains($"comment:{comment.Id}");
        var body = BodyOf(comment, inner);
        var cuttable = body.Count > bodyLines;
        var target = new DetailTarget(DetailTargetKind.Comment, comment.Id);
        var header = target with { Expands = cuttable && !expanded, Collapses = cuttable && expanded };

        rows.Add(new CommentRow(Bar(comment, detail, inner), header, true, index));

        var content = new PanelContent();

        var limit = new LongTextLimit(bodyLines, inner, ZoomHint: bodyLines == BodyLines);

        LongText.Add(content, body, target, expanded, limit);

        for (var line = 0; line < content.Lines.Count; line++)
        {
            rows.Add(new CommentRow(content.Lines[line], content.Targets[line], expanded, index));
        }

        rows.Add(new CommentRow(StyledLine.Empty, null, false, index));
    }

    private static List<StyledLine> BodyOf(IssueComment comment, int inner)
    {
        if (comment.Body.Content.Count == 0)
        {
            return [StyledLine.Of(EmptyCommentText, TextRole.Muted)];
        }

        return [.. AdfRenderer.Render(comment.Body, inner).Lines];
    }

    /// <summary>The header bar: author left, time and <c>edited</c> right, one tint across the inner width.</summary>
    private static StyledLine Bar(IssueComment comment, DetailState detail, int inner)
    {
        var right = Time(comment, detail);
        var rightWidth = DisplayWidth.Of(right);
        var room = Math.Max(1, inner - (rightWidth == 0 ? 0 : rightWidth + BarGap));
        var author = DisplayText.Cut(TextClean.Of(comment.Author?.DisplayName ?? UnknownAuthor), room);
        var fill = Math.Max(0, inner - DisplayWidth.Of(author) - rightWidth);
        var spans = new List<StyledSpan>
        {
            new(author, TextRole.ValueBlock),
            new(new string(' ', fill), TextRole.ValueBlock),
        };

        if (right.Length > 0)
        {
            spans.Add(new StyledSpan(right, TextRole.ValueBlock, SpanGround.Tail));
        }

        return new StyledLine(spans);
    }

    private static string Time(IssueComment comment, DetailState detail)
    {
        var text = string.Empty;

        if (comment.Created is { } created && detail.LoadedAt is { } now)
        {
            text = RelativeTime.Format(created, now);
        }

        if (comment.Created is { } first && comment.Updated is { } last && last - first > _editedAfter)
        {
            return text.Length == 0 ? EditedText : $"{text} · {EditedText}";
        }

        return text;
    }

    private static void AddFooter(List<CommentRow> rows, DetailState detail)
    {
        switch (detail.CommentsLoad)
        {
            case Loading when detail.Comments.Count == 0:
                rows.Add(Plain(LoadingText, TextRole.Loading));
                break;
            case Loading:
                rows.Add(Plain(LoadingMoreText, TextRole.Loading));
                break;
            case Failed failed:
                rows.Add(Plain($"{failed.Message}{RetryText}", TextRole.Error));
                break;
            default:
                AddIdleFooter(rows, detail);
                break;
        }
    }

    private static void AddIdleFooter(List<CommentRow> rows, DetailState detail)
    {
        if (detail.NextComments is not null)
        {
            rows.Add(Plain(LoadMoreText, TextRole.Link));
        }
        else if (detail.Comments.Count == 0)
        {
            rows.Add(Plain(NoCommentsText, TextRole.Muted));
        }
        else if (detail.CommentTotal is { } total && total > detail.Comments.Count)
        {
            rows.Add(Plain(detail.CommentsNewestFirst ? TooManyNewestText : TooManyOldestText, TextRole.Muted));
        }
    }

    /// <summary>A footer row; the cursor can rest on it so that the pane can be focused while it holds no comment.</summary>
    private static CommentRow Plain(string text, TextRole role)
    {
        return new CommentRow(StyledLine.Of(text, role), null, true, -1);
    }
}
