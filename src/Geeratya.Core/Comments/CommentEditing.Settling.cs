using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Comments;

internal static partial class CommentEditing
{
    /// <summary>
    /// Settles the editor and the comment list after the comment write of <paramref name="command"/> ended. Other
    /// writes, and a write that is only being checked after a lost reply, leave <paramref name="result"/> as it is.
    /// </summary>
    public static ReduceResult Settled(AppState before, ReduceResult result, WriteCommand command)
    {
        var operation = OperationOf(before, command);

        if (operation is null || result.State.Session.Operations.Any(other => other.Id == operation.Id))
        {
            return result;
        }

        var applied = command is WriteSucceeded or WriteVerified { Outcome: VerifyOutcome.Applied };

        return applied
            ? Applied(result, (CommentRequest)operation.Request, operation, StoredComment(command))
            : result with { State = Rejected(result.State, operation) };
    }

    private static Operation? OperationOf(AppState state, WriteCommand command)
    {
        long? id = command switch
        {
            WriteSucceeded succeeded => succeeded.OperationId,
            WriteFailed failed => failed.OperationId,
            WriteVerified verified => verified.OperationId,
            _ => null,
        };

        var operation = id is null ? null : state.Session.Operations.FirstOrDefault(other => other.Id == id);

        return operation?.Request is CommentRequest ? operation : null;
    }

    private static IssueComment? StoredComment(WriteCommand command)
    {
        return command switch
        {
            WriteSucceeded succeeded => succeeded.Comment,
            WriteVerified verified => verified.Comment,
            _ => null,
        };
    }

    private static ReduceResult Applied(
        ReduceResult result,
        CommentRequest request,
        Operation operation,
        IssueComment? stored)
    {
        var effects = result.Effects.ToList();
        var state = ClosedFor(result.State, operation);
        var withoutReply = stored is null && request is not DeleteCommentRequest;

        if (withoutReply)
        {
            // The reply did not carry the comment: read the first page again.
            state = DetailReducer.ReloadComments(state, operation.Origin, request.Key, effects);
        }
        else
        {
            state = DetailReducer.ChangeIssues(state, request.Key, detail => WithComment(detail, request, stored));
        }

        return new ReduceResult(state, effects);
    }

    /// <summary>The comment list of <paramref name="detail"/> after the write: the stored comment, or one removed.</summary>
    private static DetailState WithComment(DetailState detail, CommentRequest request, IssueComment? stored)
    {
        if (request is DeleteCommentRequest delete)
        {
            var remaining = detail.Comments.Where(comment => comment.Id != delete.CommentId).ToArray();
            var removed = remaining.Length < detail.Comments.Count;

            return detail with { Comments = remaining, CommentTotal = Counted(detail.CommentTotal, removed ? -1 : 0) };
        }

        if (stored is null)
        {
            return detail;
        }

        var known = detail.Comments.Any(comment => comment.Id == stored.Id);

        if (known)
        {
            var replaced = detail.Comments.Select(comment => comment.Id == stored.Id ? stored : comment).ToArray();

            return detail with { Comments = replaced };
        }

        // Newest first: the added comment is the newest, so it goes on top whatever pages are still unloaded.
        return detail with { Comments = [stored, .. detail.Comments], CommentTotal = Counted(detail.CommentTotal, 1) };
    }

    private static int? Counted(int? total, int change)
    {
        return total is { } known ? Math.Max(0, known + change) : null;
    }

    /// <summary>The write ended without effect: the editor (if it started this write) shows why and stays open.</summary>
    private static AppState Rejected(AppState state, Operation operation)
    {
        if (state.Editor is not { } editor || editor.OperationId != operation.Id)
        {
            return state;
        }

        var shown = editor with { Error = state.Message, OperationId = 0 };

        return state with { Editor = shown, Message = null, Status = null };
    }

    private static AppState ClosedFor(AppState state, Operation operation)
    {
        return state.Editor is { } editor && editor.OperationId == operation.Id ? Closed(state) : state;
    }

    private static AppState Closed(AppState state)
    {
        return state with { Mode = InputMode.Normal, Editor = null, Confirm = null, PendingKeys = string.Empty };
    }

    private static CommentRequest RequestFor(AppState state, CommentEditor editor, DetailState detail)
    {
        var body = AdfMarkdownParser.Parse(editor.Buffer.Text);

        if (editor.CommentId is { } id)
        {
            return new EditCommentRequest(editor.Key, id, body);
        }

        var author = state.Session.Connection is Connected { AccountId: { } account } ? account : null;

        return new AddCommentRequest(editor.Key, body, author, HighestId(detail));
    }

    private static long HighestId(DetailState detail)
    {
        long highest = 0;

        foreach (var comment in detail.Comments)
        {
            if (long.TryParse(comment.Id, NumberStyles.None, CultureInfo.InvariantCulture, out var id))
            {
                highest = Math.Max(highest, id);
            }
        }

        return highest;
    }

    // Every distinct account the comment mentions, in order of appearance.
    private static List<MentionedPerson> MentionedIn(AdfDocument body)
    {
        var people = new List<MentionedPerson>();

        foreach (var node in body.Content.SelectMany(Walk))
        {
            if (node is AdfMention { Id: { Length: > 0 } id } mention && people.TrueForAll(known => known.Id != id))
            {
                // Another author wrote the name: no control characters, and without a name there is nothing to look up.
                var name = Clean(mention.Text ?? string.Empty).TrimStart('@').Trim();

                if (name.Length > 0)
                {
                    people.Add(new MentionedPerson(id, name));
                }
            }
        }

        return people;
    }

    private static IEnumerable<AdfNode> Walk(AdfNode node)
    {
        yield return node;

        if (node is AdfContainerNode container)
        {
            foreach (var inner in container.Children.SelectMany(Walk))
            {
                yield return inner;
            }
        }
    }

    private static string? Warning(AppState state, IssueComment comment)
    {
        var warnings = new List<string>();

        if (!AdfRoundTrip.RoundTrips(comment.Body))
        {
            warnings.Add(LostFormattingText);
        }

        if (IsOthers(state, comment.Author))
        {
            warnings.Add(NotYoursText);
        }

        return warnings.Count == 0 ? null : string.Join(' ', warnings);
    }

    private static bool IsOthers(AppState state, IssueAssignee? author)
    {
        return state.Session.Connection is Connected { AccountId: { } mine }
            && author?.AccountId is { } theirs
            && !string.Equals(mine, theirs, StringComparison.Ordinal);
    }
}
