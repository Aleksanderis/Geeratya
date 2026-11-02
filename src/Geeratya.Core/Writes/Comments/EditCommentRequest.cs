using Geeratya.Jira.Adf;

namespace Geeratya.Core.Writes.Comments;

/// <summary>Replace the body of comment <paramref name="CommentId"/> of <paramref name="Key"/>.</summary>
public sealed record EditCommentRequest(string Key, string CommentId, AdfDocument Body) : CommentRequest(Key)
{
    public override OperationKind Kind => OperationKind.EditComment;

    public override string BusyText => "editing a comment";

    public override string StartedText => $"Saving the comment on {Key}";

    public override string DoneText => $"Saved the comment on {Key}";

    public override string CheckingText => $"{Key}: not sure the comment was saved, checking";

    public override string NotAppliedText => $"{Key}: the comment was not changed";

    public override string ActionText => $"edit the comment on {Key}";
}
