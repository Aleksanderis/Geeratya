using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.App;

/// <summary>
/// Sends the comment writes (add, edit, delete) and checks them after a lost reply. The check reads the comments of
/// the issue (a direct read, not the search index, so it does not lag): a comment that is there proves an add or an
/// edit, a missing one proves a delete, and a read that did not see the whole thread proves nothing.
/// </summary>
internal static class CommentWriter
{
    /// <summary>The pages the check reads at most (the same cap as the detail screen).</summary>
    private const int MaxPages = DetailPaging.MaxComments / CommentClient.MaxPageSize;

    public static async Task<CommentReply> SendAsync(
        JiraServices services,
        CommentRequest request,
        CancellationToken token)
    {
        switch (request)
        {
            case AddCommentRequest add:
                return Reply(
                    add.ParentId is { } parent
                        ? await services.Comments.ReplyToCommentAsync(add.Key, parent, add.Body, token)
                        : await services.Comments.AddCommentAsync(add.Key, add.Body, token));
            case EditCommentRequest edit:
                return Reply(await services.Comments.EditCommentAsync(edit.Key, edit.CommentId, edit.Body, token));
            case DeleteCommentRequest delete:
                var deleted = await services.Comments.DeleteCommentAsync(delete.Key, delete.CommentId, token);

                return deleted.IsSuccess ? new CommentReply(null, null) : new CommentReply(deleted.Error, null);
            default:
                throw new NotSupportedException($"No handler for {request.GetType().Name}.");
        }
    }

    public static async Task<CommentCheck> VerifyAsync(
        JiraServices services,
        CommentRequest request,
        CancellationToken token)
    {
        try
        {
            var thread = await ReadAsync(services, request.Key, token);

            return thread is null ? Unreachable : Decide(request, thread);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
            return Unreachable;
        }
    }

    private static CommentCheck Unreachable => new(VerifyOutcome.Unreachable, null);

    private static CommentReply Reply(WriteResult<IssueComment> result)
    {
        return result.IsSuccess ? new CommentReply(null, result.Value) : new CommentReply(result.Error, null);
    }

    private static CommentCheck Decide(CommentRequest request, CommentThread thread)
    {
        var found = request switch
        {
            AddCommentRequest add => thread.Comments.FirstOrDefault(comment => IsNew(comment, add)),
            EditCommentRequest edit => thread.Comments.FirstOrDefault(
                comment => comment.Id == edit.CommentId && SameText(comment.Body, edit.Body)),
            DeleteCommentRequest delete => thread.Comments.FirstOrDefault(comment => comment.Id == delete.CommentId),
            _ => null,
        };

        if (request is DeleteCommentRequest)
        {
            // A comment that is still there was not deleted; one that is missing is gone only if the whole thread was read.
            if (found is not null)
            {
                return new CommentCheck(VerifyOutcome.NotApplied, null);
            }

            return thread.IsComplete ? new CommentCheck(VerifyOutcome.Applied, null) : Unreachable;
        }

        if (found is not null)
        {
            return new CommentCheck(VerifyOutcome.Applied, found);
        }

        return thread.IsComplete ? new CommentCheck(VerifyOutcome.NotApplied, null) : Unreachable;
    }

    private static bool IsNew(IssueComment comment, AddCommentRequest add)
    {
        var newer = long.TryParse(comment.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            && id > add.AfterId;
        var mine = add.AuthorAccountId is null || comment.Author?.AccountId == add.AuthorAccountId;


        // The parent is not compared: Jira may ignore parentId, and a stored comment must not be reported missing.
        return newer && mine && SameText(comment.Body, add.Body);
    }

    /// <summary>Compares the text form, which survives the changes Jira makes to a stored body.</summary>
    private static bool SameText(AdfDocument stored, AdfDocument sent)
    {
        return string.Equals(
            AdfMarkdownWriter.ToText(stored).Trim(),
            AdfMarkdownWriter.ToText(sent).Trim(),
            StringComparison.Ordinal);
    }

    private static async Task<CommentThread?> ReadAsync(JiraServices services, string key, CancellationToken token)
    {
        var comments = new List<IssueComment>();
        var startAt = 0;

        for (var page = 0; page < MaxPages; page++)
        {
            var result = await services.Comments.GetCommentsAsync(key, startAt, token);

            if (!result.IsSuccess)
            {
                return null;
            }

            comments.AddRange(result.Value.Items);

            if (result.Value.NextStartAt is not { } next)
            {
                return new CommentThread(comments, true);
            }

            startAt = next;
        }

        return new CommentThread(comments, false);
    }

    private sealed record CommentThread(IReadOnlyList<IssueComment> Comments, bool IsComplete);
}

/// <summary>The reply of a comment write: the error, or the stored comment (null for a delete).</summary>
internal sealed record CommentReply(WriteError? Error, IssueComment? Comment);

/// <summary>What the check of a comment write found.</summary>
internal sealed record CommentCheck(VerifyOutcome Outcome, IssueComment? Comment);
