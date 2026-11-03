using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Editing;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Adf;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Comments;

/// <summary>
/// The comment part of the reducer (<c>docs/ux-screens.md</c>, "Issue detail"): opens the editor for a new comment or
/// for the comment under the cursor, edits its text, asks before throwing text away or deleting a comment, starts the
/// write, and applies the settled write to the editor and to the comment list. Pure. Adding and editing wait for the
/// reply (no optimistic text): the editor shows <c>Saving…</c> and stays open on an error with the text intact.
/// </summary>
internal static class CommentEditing
{
    /// <summary>The confirm action that throws the editor text away.</summary>
    public const string DiscardAction = "comment.discard";

    private const string OpenIssueText = "Open an issue first.";
    private const string NoCommentText = "Put the cursor on a comment first.";
    private const string EmptyText = "A comment cannot be empty.";
    private const string NoChangeText = "No change to save.";
    private const string LostFormattingText =
        "This comment has formatting the editor cannot show; saving replaces it with the text below.";
    private const string NotYoursText = "Not your comment: Jira refuses unless you may edit all comments.";
    private const int QuoteLength = 30;
    private const string Ellipsis = "…";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary><c>comment.add</c>: opens an empty editor for the shown issue.</summary>
    public static ReduceResult Add(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return Say(state, OpenIssueText);
        }

        var editor = new CommentEditor(top.Tab.Id, top.Detail.Key, null, TextBuffer.Empty, string.Empty);

        return Result(WithOpenEditor(state, editor));
    }

    /// <summary><c>comment.edit</c>: opens the editor on the comment under the cursor.</summary>
    public static ReduceResult Edit(AppState state)
    {
        if (TopOf(state) is not { } top)
        {
            return Say(state, OpenIssueText);
        }

        if (CommentUnderCursor(top.Detail) is not { } comment)
        {
            return Say(state, NoCommentText);
        }

        // Another author wrote it: control characters (escape sequences, tabs, CR) must not reach the screen.
        var text = PasteText.Lines(AdfMarkdownWriter.ToText(comment.Body));
        var editor = new CommentEditor(
            top.Tab.Id,
            top.Detail.Key,
            comment.Id,
            TextBuffer.Of(text),
            text,
            Warning(state, comment));
        var people = MentionedIn(comment.Body);
        var effects = people.Count == 0
            ? _noEffects
            : (IReadOnlyList<Effect>)[new CheckMentionsEffect(top.Tab.Id, top.Detail.Key, comment.Id, people)];

        return new ReduceResult(WithOpenEditor(state, editor), effects);
    }

    /// <summary><c>comment.delete</c>: asks first, naming the comment and that Jira cannot undo the delete.</summary>
    public static ReduceResult Delete(AppState state, DateTimeOffset at)
    {
        if (TopOf(state) is not { } top)
        {
            return Say(state, OpenIssueText);
        }

        if (CommentUnderCursor(top.Detail) is not { } comment)
        {
            return Say(state, NoCommentText);
        }

        var author = comment.Author?.DisplayName ?? "unknown";
        var message =
            $"Delete the {top.Detail.Key} comment? Jira cannot undo this. By {Clean(author)}: \"{Quote(comment)}\"";
        var write = new DeleteCommentRequest(top.Detail.Key, comment.Id);
        var confirmed = new WriteRequested(top.Tab.Id, write, at);
        var request = new ConfirmRequest(message, ActionIds.CommentDelete, comment.Id, confirmed, "Delete comment");

        return Result(state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty });
    }

    /// <summary>Types text (or a paste) at the cursor.</summary>
    public static AppState Type(AppState state, string text)
    {
        return Change(state, buffer => buffer.Insert(text));
    }

    public static AppState Backspace(AppState state)
    {
        return Change(state, static buffer => buffer.Backspace());
    }

    public static AppState DeleteForward(AppState state)
    {
        return Change(state, static buffer => buffer.Delete());
    }

    public static AppState Move(AppState state, EditorMove move)
    {
        var capacity = EditorLayout.Capacity(CommentEditor.InnerWidth(state.ViewportColumns));

        var page = Math.Max(1, state.ViewportRows / 2);

        return Change(state, buffer => Moved(buffer, move, capacity, page));
    }

    /// <summary>
    /// Esc: closes an editor whose text is unchanged; otherwise asks before the text is thrown away. While the save
    /// runs nothing is closed (the write cannot be cancelled).
    /// </summary>
    public static ReduceResult Cancel(AppState state)
    {
        if (state.Editor is not { } editor || editor.IsSaving)
        {
            return Result(state);
        }

        if (!editor.IsChanged)
        {
            return Result(Closed(state));
        }

        var request = new ConfirmRequest(
            "Discard the unsent comment text? It is not saved anywhere",
            DiscardAction,
            Label: "Discard text");

        return Result(state with { Mode = InputMode.Confirm, Confirm = request, PendingKeys = string.Empty });
    }

    /// <summary>The answer to the discard question was no: back to the editor with the text as it was.</summary>
    public static AppState KeepEditing(AppState state)
    {
        return state with { Mode = InputMode.Insert, Confirm = null, PendingKeys = string.Empty };
    }

    /// <summary><c>form.submit</c> (Ctrl-s): starts the write, or says in the editor why it did not start.</summary>
    public static ReduceResult Save(AppState state, DateTimeOffset at)
    {
        if (state.Editor is not { IsSaving: false } editor || TopOf(state) is not { } top)
        {
            return Result(state);
        }

        var text = editor.Buffer.Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result(WithEditor(state, editor with { Error = EmptyText }));
        }

        if (editor.IsEdit && !editor.IsChanged)
        {
            return Say(Closed(state), NoChangeText);
        }

        var request = RequestFor(state, editor, top.Detail);
        var started = WriteReducer.Reduce(state, new WriteRequested(editor.Tab, request, at));

        if (started.State.Sequence == state.Sequence)
        {
            // Refused (the issue is busy, no profile): the editor stays, the reason shows in it.
            var refused = WithEditor(state, editor with { Error = started.State.Message });

            return Result(refused);
        }

        var saving = WithEditor(started.State, editor with { Error = null, OperationId = started.State.Sequence });

        return new ReduceResult(saving, started.Effects);
    }

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

        // A comment added while later pages are still unloaded is not shown out of place: "Load more" brings it.
        var appended = detail.NextComments is null ? [.. detail.Comments, stored] : detail.Comments;

        return detail with { Comments = appended, CommentTotal = Counted(detail.CommentTotal, 1) };
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

        return state with { Editor = shown, Message = null };
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

    private static TextBuffer Moved(TextBuffer buffer, EditorMove move, int capacity, int page)
    {
        return move switch
        {
            EditorMove.Left => buffer.Left(),
            EditorMove.Right => buffer.Right(),
            EditorMove.Up => buffer.Vertical(-1, capacity),
            EditorMove.Down => buffer.Vertical(1, capacity),
            EditorMove.RowStart => buffer.RowStart(capacity),
            EditorMove.PageUp => buffer.Vertical(-page, capacity),
            EditorMove.PageDown => buffer.Vertical(page, capacity),
            EditorMove.WordLeft => buffer.WordLeft(),
            EditorMove.WordRight => buffer.WordRight(),
            _ => buffer.RowEnd(capacity),
        };
    }

    private static AppState Change(AppState state, Func<TextBuffer, TextBuffer> change)
    {
        if (state.Editor is not { IsSaving: false } editor)
        {
            return state;
        }

        var buffer = change(editor.Buffer);

        if (buffer == editor.Buffer)
        {
            return state;
        }

        var edited = buffer.Text != editor.Buffer.Text;

        return WithEditor(state, editor with { Buffer = buffer, Error = edited ? null : editor.Error });
    }

    /// <summary>
    /// Whether the cursor of the shown issue is on a comment (the right-click menu offers its delete then).
    /// </summary>
    public static bool IsOnComment(AppState state)
    {
        return TopOf(state) is { } top && CommentUnderCursor(top.Detail) is not null;
    }

    private static IssueComment? CommentUnderCursor(DetailState detail)
    {
        if (DetailReducer.TargetOf(detail) is not { Kind: DetailTargetKind.Comment } target)
        {
            return null;
        }

        return detail.Comments.FirstOrDefault(comment => comment.Id == target.Value);
    }

    private static string Quote(IssueComment comment)
    {
        var text = AdfMarkdownWriter.ToText(comment.Body).ReplaceLineEndings(" ").Trim();
        var clean = Clean(text);

        return clean.Length <= QuoteLength ? clean : $"{clean[..QuoteLength].TrimEnd()}{Ellipsis}";
    }

    private static string Clean(string text)
    {
        if (!text.Any(char.IsControl))
        {
            return text;
        }

        return string.Concat(text.Select(static character => char.IsControl(character) ? ' ' : character));
    }

    private static AppState WithOpenEditor(AppState state, CommentEditor editor)
    {
        return state with
        {
            Mode = InputMode.Insert,
            Editor = editor,
            PendingKeys = string.Empty,
            Message = null,
        };
    }

    private static AppState WithEditor(AppState state, CommentEditor editor)
    {
        return state with { Editor = editor };
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return Result(state with { Message = message });
    }

    private static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }

    private static Opened? TopOf(AppState state)
    {
        var tab = state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;

        return tab is { Detail.Count: > 0 } ? new Opened(tab, tab.Detail[^1]) : null;
    }

    private sealed record Opened(TabState Tab, DetailState Detail);
}
