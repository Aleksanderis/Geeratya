using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>One row of the Comments pane's content, before the pane shows its window of them.</summary>
/// <param name="Line">The styled row, at most the pane's inner width wide.</param>
/// <param name="Target">What Enter and the comment keys act on, null for a plain row.</param>
/// <param name="IsStop">Whether the cursor can rest on the row.</param>
/// <param name="Comment">The index of the comment the row belongs to, -1 for the footer.</param>
public sealed record CommentRow(StyledLine Line, DetailTarget? Target, bool IsStop, int Comment);
