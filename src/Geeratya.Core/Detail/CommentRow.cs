using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>Produces the styled row of a <see cref="CommentRow"/> when it is first read (see <see cref="CommentRow"/>).</summary>
public interface ICommentRowSource
{
    /// <summary>The styled row.</summary>
    StyledLine Resolve();
}

/// <summary>
/// One row of the Comments pane's content, before the pane shows its window of them. The row of a cut comment's
/// <c>… N more lines</c> note is made when it is first read, so a layout that shows a window of the rows
/// wraps the hidden lines of the comments in that window only (#950).
/// </summary>
public sealed record CommentRow
{
    private readonly ICommentRowSource? _source;
    private StyledLine? _line;

    /// <summary>A row with its styled line.</summary>
    /// <param name="line">The styled row, at most the pane's inner width wide.</param>
    /// <param name="target">What Enter and the comment keys act on, null for a plain row.</param>
    /// <param name="isStop">Whether the cursor can rest on the row.</param>
    /// <param name="comment">The index of the comment the row belongs to, -1 for the footer.</param>
    public CommentRow(StyledLine line, DetailTarget? target, bool isStop, int comment)
    {
        _line = line;
        Target = target;
        IsStop = isStop;
        Comment = comment;
    }

    /// <summary>A row whose styled line is made by <paramref name="source"/> when it is first read.</summary>
    /// <param name="source">Makes the styled line.</param>
    /// <param name="target">What Enter and the comment keys act on, null for a plain row.</param>
    /// <param name="isStop">Whether the cursor can rest on the row.</param>
    /// <param name="comment">The index of the comment the row belongs to, -1 for the footer.</param>
    public CommentRow(ICommentRowSource source, DetailTarget? target, bool isStop, int comment)
    {
        _source = source;
        Target = target;
        IsStop = isStop;
        Comment = comment;
    }

    /// <summary>The styled row, at most the pane's inner width wide.</summary>
    public StyledLine Line => _line ??= _source!.Resolve();

    /// <summary>What Enter and the comment keys act on, null for a plain row.</summary>
    public DetailTarget? Target { get; }

    /// <summary>Whether the cursor can rest on the row.</summary>
    public bool IsStop { get; }

    /// <summary>The index of the comment the row belongs to, -1 for the footer.</summary>
    public int Comment { get; }
}
