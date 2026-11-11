using System.Runtime.CompilerServices;
using Geeratya.Core.Adf;
using Geeratya.Core.Views;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;

namespace Geeratya.Core.Detail;

/// <summary>
/// What one opened issue has rendered, kept so a key press does not render it again (#929): the comment rows of the
/// Comments pane (one entry) and the ADF lines of the description and of every comment body per width. The inputs
/// are compared by reference (the comment list, the documents) or value (width, cut, order, load state), so changed
/// data always renders again; the memo owns no invalidation rule of its own and is dropped with the issue. Copies of a
/// <see cref="DetailState"/> share it. Used from one thread only (the reducer or the frame that follows it).
/// </summary>
public sealed class DetailRenderMemo
{
    private const int MinEntries = 16;
    private const int EntriesPerDocument = 4;

    private readonly Dictionary<RenderKey, AdfRendering> _renderings = [];
    private readonly Dictionary<HeadKey, AdfHead> _heads = [];
    private IdsEntry? _ids;
    private RowsEntry? _rows;

    /// <summary>
    /// The comment rows for these inputs, built by <see cref="CommentRows.Build"/> only when they differ from the last
    /// call.
    /// </summary>
    public IReadOnlyList<CommentRow> Rows(DetailState detail, int inner, int bodyLines)
    {
        var last = _rows;

        if (last is not null && last.Matches(detail, inner, bodyLines))
        {
            return last.Rows;
        }

        var rows = CommentRows.Build(detail, inner, bodyLines, this);

        _rows = new RowsEntry(detail, inner, bodyLines, rows);

        return rows;
    }

    /// <summary>
    /// The ADF lines of <paramref name="document"/> for <paramref name="width"/>, rendered once per pair.
    /// <paramref name="documents"/> is how many documents the issue has, which bounds the entries kept.
    /// </summary>
    public AdfRendering Rendering(AdfDocument document, int width, int documents)
    {
        var key = new RenderKey(document, width);

        if (_renderings.TryGetValue(key, out var known))
        {
            return known;
        }

        var rendering = AdfRenderer.Render(document, width);

        Store(key, rendering, documents);

        return rendering;
    }

    /// <summary>How many whole renderings are kept (tests check that a cut comment keeps none until its note is read).</summary>
    public int WholeRenderings => _renderings.Count;

    private void Store(RenderKey key, AdfRendering rendering, int documents)
    {
        // A refresh brings new documents, so the old entries go once there are more than a few widths of them.
        if (_renderings.Count > Math.Max(MinEntries, documents * EntriesPerDocument))
        {
            _renderings.Clear();
        }

        _renderings[key] = rendering;
    }

    /// <summary>
    /// The start of the rendering of <paramref name="document"/> (<see cref="AdfRenderer.RenderHead"/>), once per
    /// width and line limit; <c>Complete</c> is false when the document has more than <paramref name="maxLines"/> lines.
    /// </summary>
    public AdfHead Head(AdfDocument document, int width, int maxLines, int documents)
    {
        var key = new HeadKey(document, width, maxLines);

        if (_heads.TryGetValue(key, out var known))
        {
            return known;
        }

        if (_heads.Count > Math.Max(MinEntries, documents * EntriesPerDocument))
        {
            _heads.Clear();
        }

        var head = AdfRenderer.RenderHead(document, width, maxLines);

        _heads[key] = head;

        // A document that fits the limit was rendered whole: keep it for Rendering instead of laying it out again.
        if (head.Complete)
        {
            Store(new RenderKey(document, width), head.Rendering, documents);
        }

        return head;
    }

    /// <summary>
    /// The comment id of every one of <paramref name="rows"/> (null for rows of no comment), made once per row list:
    /// the list is the one <see cref="Rows"/> returns, so a key that changes no rows copies nothing.
    /// </summary>
    public IReadOnlyList<string?> IdsOf(IReadOnlyList<CommentRow> rows)
    {
        if (_ids is { } last && ReferenceEquals(last.Rows, rows))
        {
            return last.Ids;
        }

        var ids = new string?[rows.Count];

        for (var index = 0; index < ids.Length; index++)
        {
            ids[index] = rows[index].Target is { Kind: DetailTargetKind.Comment } target ? target.Value : null;
        }

        _ids = new IdsEntry(rows, ids);

        return ids;
    }

    /// <summary>The memo is a cache, not state: two details that differ only in it are equal.</summary>
    public override bool Equals(object? obj)
    {
        return obj is DetailRenderMemo;
    }

    /// <summary>See <see cref="Equals(object?)"/>.</summary>
    public override int GetHashCode()
    {
        return 0;
    }

    private sealed record IdsEntry(IReadOnlyList<CommentRow> Rows, IReadOnlyList<string?> Ids);

    private readonly record struct HeadKey(AdfDocument Document, int Width, int Lines)
    {
        public bool Equals(HeadKey other)
        {
            return ReferenceEquals(Document, other.Document) && Width == other.Width && Lines == other.Lines;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RuntimeHelpers.GetHashCode(Document), Width, Lines);
        }
    }

    private readonly record struct RenderKey(AdfDocument Document, int Width)
    {
        public bool Equals(RenderKey other)
        {
            return ReferenceEquals(Document, other.Document) && Width == other.Width;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RuntimeHelpers.GetHashCode(Document), Width);
        }
    }

    private sealed class RowsEntry
    {
        private readonly int _inner;
        private readonly int _bodyLines;
        private readonly IReadOnlyList<IssueComment> _comments;
        private readonly IReadOnlyList<string> _expanded;
        private readonly bool _newestFirst;
        private readonly DateTimeOffset? _loadedAt;
        private readonly LoadState _load;
        private readonly int? _next;
        private readonly int? _total;

        public RowsEntry(DetailState detail, int inner, int bodyLines, IReadOnlyList<CommentRow> rows)
        {
            _inner = inner;
            _bodyLines = bodyLines;
            _comments = detail.Comments;
            _expanded = detail.Expanded;
            _newestFirst = detail.CommentsNewestFirst;
            _loadedAt = detail.LoadedAt;
            _load = detail.CommentsLoad;
            _next = detail.NextComments;
            _total = detail.CommentTotal;
            Rows = rows;
        }

        public IReadOnlyList<CommentRow> Rows { get; }

        public bool Matches(DetailState detail, int inner, int bodyLines)
        {
            return _inner == inner
                && _bodyLines == bodyLines
                && ReferenceEquals(_comments, detail.Comments)
                && _expanded.SequenceEqual(detail.Expanded)
                && _newestFirst == detail.CommentsNewestFirst
                && _loadedAt == detail.LoadedAt
                && _load == detail.CommentsLoad
                && _next == detail.NextComments
                && _total == detail.CommentTotal;
        }
    }
}
