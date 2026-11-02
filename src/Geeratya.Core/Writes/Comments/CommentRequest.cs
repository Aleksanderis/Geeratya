using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes.Comments;

/// <summary>
/// A write on the comments of one issue. It changes no row and no header field, so the pipeline skips the row
/// patch, the search read and the issue reload; the handler reads the comments instead, and the reducer updates the
/// comment list of the open issue from the reply.
/// </summary>
public abstract record CommentRequest(string Key) : WriteRequest
{
    public override IReadOnlyList<string> Keys => [Key];

    public override bool ChangesRows => false;

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        return null;
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return false;
    }
}
