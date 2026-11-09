using Geeratya.Jira.Adf;

namespace Geeratya.Core.Writes.Comments;

/// <summary>Add a comment with <paramref name="Body"/> to <paramref name="Key"/>.</summary>
/// <param name="Key">The issue.</param>
/// <param name="Body">The comment as ADF.</param>
/// <param name="AuthorAccountId">The account that writes it; null when not known. Used to find it after a lost reply.</param>
/// <param name="AfterId">
/// The highest comment id the issue showed when the write started (0 when none); a comment at or below it is not the
/// new one. Used to find the new comment after a lost reply.
/// </param>
/// <param name="ParentId">The top-level comment this one replies to (sent as <c>parentId</c>); null for a new thread.</param>
public sealed record AddCommentRequest(
    string Key,
    AdfDocument Body,
    string? AuthorAccountId,
    long AfterId,
    string? ParentId = null)
    : CommentRequest(Key)
{
    public override OperationKind Kind => OperationKind.AddComment;

    public override string BusyText => "commenting";

    public override string StartedText => $"Adding a comment to {Key}";

    public override string DoneText => $"Added a comment to {Key}";

    public override string CheckingText => $"{Key}: not sure the comment was added, checking";

    public override string NotAppliedText => $"{Key}: the comment was not added";

    public override string ActionText => $"add a comment to {Key}";
}
