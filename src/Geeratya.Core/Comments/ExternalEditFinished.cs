using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Comments;

/// <summary>
/// The outside editor ended. <paramref name="Text"/> is the edited text, or null when nothing changed or the editor
/// failed; <paramref name="Error"/> says why it failed (null otherwise). The three identity fields name the editor
/// that asked, so a reply that arrives after the editor was closed or replaced is dropped.
/// </summary>
public sealed record ExternalEditFinished(
    TabId Tab,
    string Key,
    string? CommentId,
    string? Text,
    string? Error) : Command;
