using System.Globalization;
using System.Text.Json;
using Geeratya.Core.Links;
using Geeratya.Core.Writes;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.App;

public sealed partial class WriteHandler
{
    private static async Task<WriteResult<WriteAccepted>> SendAsync(
        JiraServices services,
        WriteRequest request,
        CancellationToken token)
    {
        if (request is AssignRequest assign)
        {
            var change = assign.Assignee is null
                ? AssigneeChange.Unassign
                : AssigneeChange.User(assign.Assignee.AccountId);

            return await services.Edits.AssignAsync(assign.Key, change, token);
        }

        if (request is RankMoveRequest rankMove)
        {
            return await RankMoveAsync(services, rankMove, token);
        }

        if (request is RankBlockRequest block)
        {
            return await RankBlockAsync(services, block, token);
        }

        if (request is RankRequest rank)
        {
            return await RankAsync(services, rank, token);
        }

        if (request is SprintMoveRequest move)
        {
            return await SprintMoveAsync(services, move, token);
        }

        if (request is DeleteIssueRequest delete)
        {
            return await services.Edits.DeleteIssueAsync(delete.Key, delete.DeleteSubtasks, token);
        }

        if (request is LinkIssuesRequest link)
        {
            return await services.Links.CreateLinkAsync(link.TypeId, link.OutwardKey, link.InwardKey, token);
        }

        if (request is UnlinkIssuesRequest unlink)
        {
            return await services.Links.DeleteLinkAsync(unlink.LinkId, token);
        }

        if (request is EditRequest edit)
        {
            return await EditAsync(services, edit, token);
        }

        if (request is TransitionRequest transition)
        {
            return await TransitionAsync(services, transition, token);
        }

        throw new NotSupportedException($"No handler for {request.GetType().Name}.");
    }

    private static async Task<WriteResult<WriteAccepted>> TransitionAsync(
        JiraServices services,
        TransitionRequest transition,
        CancellationToken token)
    {
        Dictionary<string, JsonElement> fields;

        try
        {
            // Transition fields are plain values: the whole value is sent, never adds and removes.
            var plain = transition.Changes.Select(change => change with { Initial = null }).ToArray();

            fields = FieldUpdates.For(plain).ToDictionary(update => update.FieldId, update => update.ToElement());
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            var error = new WriteError(WriteErrorKind.Validation, null, exception.Message, [], []);

            return WriteResults.Failure<WriteAccepted>(error);
        }

        return await services.Transitions.PerformAsync(transition.Key, transition.Choice.Id, fields, null, token);
    }

    /// <summary>
    /// Moves the issues (chunks of 50, one after the other). The pipeline settles a write as a whole, so a move that
    /// did not take every issue fails with a message naming how many moved and which did not; the optimistic rows are
    /// undone and the tabs reload, which shows what really moved. A chunk with no reply is an unknown outcome.
    /// </summary>
    private static async Task<WriteResult<WriteAccepted>> SprintMoveAsync(
        JiraServices services,
        SprintMoveRequest move,
        CancellationToken token)
    {
        var result = move.Target is { } target
            ? await services.SprintMoves.MoveToSprintAsync(target.Id, move.Keys, token)
            : await services.SprintMoves.MoveToBacklogAsync(move.Keys, token);

        if (!result.IsSuccess)
        {
            return WriteResults.Failure<WriteAccepted>(result.Error);
        }

        var outcome = result.Value;

        if (outcome.AllMoved)
        {
            return WriteResults.Success(new WriteAccepted());
        }

        if (outcome.Unknown.Count > 0)
        {
            var lost = outcome.Error ?? new WriteError(WriteErrorKind.UnknownOutcome, null, UnexpectedMessage, [], []);

            return WriteResults.Failure<WriteAccepted>(lost);
        }

        var message = SprintMoveFailureText(move, outcome);

        return WriteResults.Failure<WriteAccepted>(
            new WriteError(
                outcome.Error?.Kind ?? WriteErrorKind.Validation,
                null,
                message,
                [],
                [message],
                null,
                [.. outcome.Rejected.Select(item => item.Key).Concat(outcome.NotAttempted)]));
    }

    private static string SprintMoveFailureText(SprintMoveRequest move, SprintMoveOutcome outcome)
    {
        var failed = outcome.Rejected.Select(item => item.Key).Concat(outcome.NotAttempted).ToArray();
        var named = failed.Length <= MaxKeysNamed
            ? string.Join(", ", failed)
            : $"{string.Join(", ", failed.Take(MaxKeysNamed))} and {failed.Length - MaxKeysNamed} more";
        var reason = outcome.Rejected.SelectMany(item => item.Errors).FirstOrDefault()
            ?? outcome.Error?.Message
            ?? "Jira did not move the issue";
        var moved = outcome.Moved.Count.ToString(CultureInfo.InvariantCulture);
        var total = move.Keys.Count.ToString(CultureInfo.InvariantCulture);

        if (outcome.Moved.Count == 0 && move.Keys.Count == 1)
        {
            return reason;
        }

        return $"{moved} of {total} moved; {named} not moved: {reason}";
    }

    /// <summary>
    /// Move into the sprint of the reference issue (or the backlog), then rank next to it. A failed move stops here
    /// and nothing changed. A failed rank leaves the issues moved: the error keeps its kind (a lost reply stays unknown) and its text says
    /// the issues moved; the tabs reload and show the truth.
    /// </summary>
    private static async Task<WriteResult<WriteAccepted>> RankMoveAsync(
        JiraServices services,
        RankMoveRequest request,
        CancellationToken token)
    {
        var move = new SprintMoveRequest(request.BlockKeys, request.Target, request.SourceText);
        var moved = await SprintMoveAsync(services, move, token);

        if (!moved.IsSuccess)
        {
            return moved;
        }

        var ranked = await RankKeysAsync(
            services,
            request.BlockKeys,
            request.ReferenceKey,
            request.Before,
            request.RankFieldId,
            token);

        if (ranked.IsSuccess)
        {
            return ranked;
        }

        var error = ranked.Error;
        var message = $"{RankMoveRequest.MovedPrefix}{request.TargetText} but not ranked: {error.Message}";

        return WriteResults.Failure<WriteAccepted>(error with { Message = message, Messages = [message], FieldErrors = [] });
    }

    private static Task<WriteResult<WriteAccepted>> RankAsync(
        JiraServices services,
        RankRequest rank,
        CancellationToken token)
    {
        return RankKeysAsync(services, [rank.Key], rank.ReferenceKey, rank.Before, rank.RankFieldId, token);
    }

    private static Task<WriteResult<WriteAccepted>> RankBlockAsync(
        JiraServices services,
        RankBlockRequest block,
        CancellationToken token)
    {
        return RankKeysAsync(services, block.Keys, block.ReferenceKey, block.Before, block.RankFieldId, token);
    }

    private static async Task<WriteResult<WriteAccepted>> RankKeysAsync(
        JiraServices services,
        IReadOnlyList<string> keys,
        string referenceKey,
        bool before,
        string? rankFieldId,
        CancellationToken token)
    {
        var result = before
            ? await services.Ranks.RankBeforeAsync(keys, referenceKey, rankFieldId, token)
            : await services.Ranks.RankAfterAsync(keys, referenceKey, rankFieldId, token);

        if (!result.IsSuccess)
        {
            return WriteResults.Failure<WriteAccepted>(result.Error);
        }

        if (result.Value.AllRanked)
        {
            return WriteResults.Success(new WriteAccepted());
        }

        // A 207 that names the issue as not moved: Jira refused it, so nothing was applied.
        var failed = result.Value.Failed.ToArray();
        var messages = failed.SelectMany(item => item.Errors).ToArray();
        var message = messages.Length > 0 ? messages[0] : "Jira did not move the issue";

        if (keys.Count > 1)
        {
            var names = string.Join(", ", failed.Select(item => item.Key));

            message = $"{failed.Length} of {keys.Count} not ranked ({names}): {message}";
            messages = [message, .. messages];
        }

        return WriteResults.Failure<WriteAccepted>(
            new WriteError(WriteErrorKind.Validation, null, message, [], messages));
    }

    private static async Task<WriteResult<WriteAccepted>> EditAsync(
        JiraServices services,
        EditRequest edit,
        CancellationToken token)
    {
        IReadOnlyList<FieldUpdate> updates;

        try
        {
            updates = FieldUpdates.For(edit.Changes);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            // Nothing was sent, so this is a definite failure and not a lost reply.
            var error = new WriteError(WriteErrorKind.Validation, null, exception.Message, [], []);

            return WriteResults.Failure<WriteAccepted>(error);
        }

        return await services.Edits.UpdateAsync(edit.Key, updates, token);
    }
}
