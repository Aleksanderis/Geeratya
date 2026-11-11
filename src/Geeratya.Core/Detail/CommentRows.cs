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
    private const string Guide = $"{Panel.Vertical} ";
    private const int IndentWidth = 2;
    private const int MinThreadedWidth = 10;

    private static readonly TimeSpan _editedAfter = TimeSpan.FromMinutes(1);

    /// <param name="detail">The issue whose comments are laid out.</param>
    /// <param name="inner">The inner width of the pane.</param>
    /// <param name="bodyLines">The body lines a comment is cut after.</param>
    /// <param name="memo">Where the rendered bodies are kept between builds; null renders them every time.</param>
    public static IReadOnlyList<CommentRow> Build(
        DetailState detail,
        int inner,
        int bodyLines = BodyLines,
        DetailRenderMemo? memo = null)
    {
        var rows = new List<CommentRow>();

        var threads = CommentThreads.Arrange(detail.Comments);

        for (var index = 0; index < threads.Count; index++)
        {
            AddComment(rows, threads[index], index, new CommentContext(detail, inner, bodyLines, memo));
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

    private sealed record CommentContext(DetailState Detail, int Outer, int CutAfter, DetailRenderMemo? Memo);

    private static void AddComment(
        List<CommentRow> rows,
        ThreadedComment threaded,
        int index,
        CommentContext context)
    {
        var (detail, outer, bodyLines, memo) = context;
        var comment = threaded.Comment;
        var room = Math.Max(0, outer - MinThreadedWidth) / IndentWidth * IndentWidth;
        var indent = Math.Min(threaded.Depth * IndentWidth, room);
        var inner = outer - indent;
        var expanded = detail.Expanded.Contains($"comment:{comment.Id}");
        var limit = new LongTextLimit(bodyLines, inner, ZoomHint: bodyLines == BodyLines);
        var target = new DetailTarget(DetailTargetKind.Comment, comment.Id);
        var source = new BodySource(comment, inner, memo, detail.Comments.Count);

        // A cut comment needs only its first lines wrapped; the count in the note is made when the row is read.
        if (!expanded && comment.Body.Content.Count > 0 && LongText.NoteFitsOneRow(limit)
            && source.Head(bodyLines) is { } head)
        {
            AddCut(rows, index, indent, new CutBody(head, source, limit), target, Bar(comment, detail, inner));

            return;
        }

        var body = source.Whole();
        var cuttable = body.Count > bodyLines;
        var header = target with { Expands = cuttable && !expanded, Collapses = cuttable && expanded };

        rows.Add(new CommentRow(Indented(Bar(comment, detail, inner), indent), header, true, index));

        var content = new PanelContent();

        LongText.Add(content, body, target, expanded, limit);

        for (var line = 0; line < content.Lines.Count; line++)
        {
            rows.Add(new CommentRow(Indented(content.Lines[line], indent), content.Targets[line], expanded, index));
        }

        rows.Add(new CommentRow(StyledLine.Empty, null, false, index));
    }

    /// <summary>
    /// A reply's row with the thread guide in front (<c>│ </c> per level, a <see cref="TextRole.Border"/> span, so a
    /// yank leaves it out). A row with no content keeps no guide, so it stays a plain gap.
    /// </summary>
    private static StyledLine Indented(StyledLine line, int indent)
    {
        if (indent == 0)
        {
            return line;
        }

        var guide = string.Concat(Enumerable.Repeat(Guide, indent / IndentWidth));

        return new StyledLine([new StyledSpan(guide, TextRole.Border), .. line.Spans]);
    }

    private static void AddCut(
        List<CommentRow> rows,
        int index,
        int indent,
        CutBody body,
        DetailTarget target,
        StyledLine bar)
    {
        var (head, source, limit) = body;

        rows.Add(new CommentRow(Indented(bar, indent), target with { Expands = true }, true, index));

        for (var line = 0; line < limit.Lines; line++)
        {
            rows.Add(new CommentRow(Indented(head[line], indent), target, false, index));
        }

        rows.Add(new CommentRow(new CutNote(source, limit, indent), target with { Expands = true, Note = true }, false, index));
        rows.Add(new CommentRow(StyledLine.Empty, null, false, index));
    }

    private sealed record CutBody(IReadOnlyList<StyledLine> Head, BodySource Source, LongTextLimit Limit);

    /// <summary>
    /// The wrapped body of one comment: whole, or only its start. Reads go through the render memo, which is not
    /// thread-safe: rows are read on the UI thread only (reducer and frame).
    /// </summary>
    private sealed class BodySource(IssueComment comment, int inner, DetailRenderMemo? memo, int documents)
    {
        public IReadOnlyList<StyledLine> Whole()
        {
            if (comment.Body.Content.Count == 0)
            {
                return [StyledLine.Of(EmptyCommentText, TextRole.Muted)];
            }

            return (memo?.Rendering(comment.Body, inner, documents) ?? AdfRenderer.Render(comment.Body, inner)).Lines;
        }

        /// <summary>The first lines when the body has more than <paramref name="lines"/>, null when it does not.</summary>
        public IReadOnlyList<StyledLine>? Head(int lines)
        {
            var head = memo?.Head(comment.Body, inner, lines, documents) ?? AdfRenderer.RenderHead(comment.Body, inner, lines);

            return head.Complete ? null : head.Rendering.Lines;
        }
    }

    /// <summary>The note row of a cut comment: how many lines are hidden is counted when the row is read.</summary>
    private sealed class CutNote(BodySource source, LongTextLimit limit, int indent) : ICommentRowSource
    {
        public StyledLine Resolve()
        {
            var hidden = source.Whole().Count - limit.Lines;

            return hidden <= 0 ? StyledLine.Empty : Indented(LongText.CollapsedNote(hidden, limit) ?? StyledLine.Empty, indent);
        }
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
