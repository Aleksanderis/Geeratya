using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Comments;

/// <summary>
/// Hand the comment editor's text to the user's own editor (<c>$VISUAL</c> or <c>$EDITOR</c>): the host suspends the
/// terminal, runs the editor on a private temporary file and answers with <see cref="ExternalEditFinished"/>.
/// </summary>
/// <param name="Tab">The tab of the editor that asked.</param>
/// <param name="Key">The issue of the comment.</param>
/// <param name="CommentId">The comment being edited, null for a new comment.</param>
/// <param name="Text">The text in the editor now.</param>
public sealed record EditExternallyEffect(TabId Tab, string Key, string? CommentId, string Text) : Effect;
