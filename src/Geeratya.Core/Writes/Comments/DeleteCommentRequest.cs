namespace Geeratya.Core.Writes.Comments;

/// <summary>Delete comment <paramref name="CommentId"/> of <paramref name="Key"/>. Jira has no undo.</summary>
public sealed record DeleteCommentRequest(string Key, string CommentId) : CommentRequest(Key)
{
    public override OperationKind Kind => OperationKind.DeleteComment;

    public override string BusyText => "deleting a comment";

    public override string StartedText => $"Deleting the comment on {Key}";

    public override string DoneText => $"Deleted the comment on {Key}";

    public override string CheckingText => $"{Key}: not sure the comment was deleted, checking";

    public override string NotAppliedText => $"{Key}: the comment was not deleted";

    public override string ActionText => $"delete the comment on {Key}";
}
