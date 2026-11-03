using System.Collections.Concurrent;
using System.Text.Json;
using Geeratya.Core.Links;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;
using Geeratya.Jira.Writes;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.App;

/// <summary>
/// Turns <see cref="ExecuteWriteEffect"/> and <see cref="VerifyWriteEffect"/> into the commands that report them.
/// A write runs on the <see cref="NetworkRunner"/> detached from any tab, so closing the tab that started it never
/// cancels it (a sent write cannot be undone, and cancelling would only hide the answer); a profile switch or exit
/// does, and a reply of an older site is dropped before it is posted. A write is never repeated: a
/// lost reply becomes <see cref="WriteOutcomeUnknown"/> and the issue is read to find out.
/// </summary>
public sealed class WriteHandler
{
    private const string NoConnectionMessage = "Not connected to Jira: check the profile and its API token (:profile).";
    private const string UnexpectedMessage = "The write failed unexpectedly";

    private readonly NetworkRunner _runner;
    private readonly TimeProvider _clock;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;
    private readonly HashSet<Task> _running = [];
    private readonly Lock _lock = new();
    private readonly ConcurrentDictionary<long, DateTimeOffset> _createSubmitted = new();

    /// <param name="runner">Runs the writes.</param>
    /// <param name="clock">Stamps the replies.</param>
    /// <param name="delay">The wait of <see cref="WaitForRunningAsync"/>; tests inject one they control.</param>
    public WriteHandler(
        NetworkRunner runner,
        TimeProvider? clock = null,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _runner = runner;
        _clock = clock ?? TimeProvider.System;
        _delay = delay ?? Task.Delay;
    }

    /// <summary>True while a write or verification has been started and has not ended.</summary>
    public bool HasRunning
    {
        get
        {
            lock (_lock)
            {
                return _running.Count > 0;
            }
        }
    }

    /// <summary>
    /// Waits until every started write has ended or <paramref name="timeout"/> has passed (quit after the confirm).
    /// Returns true when none is left running.
    /// </summary>
    public async Task<bool> WaitForRunningAsync(TimeSpan timeout, CancellationToken token)
    {
        Task[] pending;

        lock (_lock)
        {
            pending = [.. _running];
        }

        if (pending.Length == 0)
        {
            return true;
        }

        using var give = CancellationTokenSource.CreateLinkedTokenSource(token);
        var all = Task.WhenAll(pending);
        var timer = _delay(timeout, give.Token);
        var first = await Task.WhenAny(all, timer);

        await give.CancelAsync();

        return first == all;
    }

    /// <summary>Starts the effect and returns the task that ends when its answer is posted or dropped.</summary>
    public Task StartAsync(Effect effect)
    {
        if (effect is CountSubtasksEffect count)
        {
            // A read that asks the question of a confirm: it is not a write, so quitting never waits for it.
            return StartCount(count);
        }

        var task = effect switch
        {
            ExecuteWriteEffect execute => StartExecute(execute),
            VerifyWriteEffect verify => StartVerify(verify),
            _ => Task.CompletedTask,
        };

        if (task.IsCompleted)
        {
            return task;
        }

        lock (_lock)
        {
            _running.Add(task);
        }

        _ = task.ContinueWith(Forget, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);

        return task;
    }

    private void Forget(Task task)
    {
        lock (_lock)
        {
            _running.Remove(task);
        }
    }

    private Task StartExecute(ExecuteWriteEffect effect)
    {
        return _runner.StartDetached(
            (services, token) => ExecuteAsync(services, effect, token),
            () => new WriteFailed(effect.OperationId, _clock.GetUtcNow(), NotSent()));
    }

    private Task StartCount(CountSubtasksEffect effect)
    {
        return _runner.StartDetached(
            (services, token) => CountAsync(services, effect, token),
            () => new SubtasksCountFailed(effect.RequestId, NoConnectionMessage));
    }

    private async Task<Command?> CountAsync(JiraServices services, CountSubtasksEffect effect, CancellationToken token)
    {
        var jql = JqlBuilder.WithOrder(JqlBuilder.SubTasksOf(effect.Key), JqlOrder.RankAscending);
        var query = new IssueSearchQuery(jql);
        var result = await services.Search.SearchAsync(query, null, token);

        if (!result.IsSuccess)
        {
            return new SubtasksCountFailed(effect.RequestId, result.Error.Message);
        }

        var page = result.Value;

        return new SubtasksCounted(effect.RequestId, page.Items.Count, !page.IsLast, _clock.GetUtcNow());
    }

    private Task StartVerify(VerifyWriteEffect effect)
    {
        return _runner.StartDetached(
            (services, token) => VerifyAsync(services, effect, token),
            () => new WriteVerified(effect.OperationId, _clock.GetUtcNow(), VerifyOutcome.Unreachable, []));
    }

    private async Task<Command?> ExecuteAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        CancellationToken token)
    {
        try
        {
            if (effect.Request is CreateRequest create)
            {
                return await CreateAsync(services, effect, create, token);
            }

            if (effect.Request is CommentRequest comment)
            {
                return await ExecuteCommentAsync(services, effect, comment, token);
            }

            var result = await SendAsync(services, effect.Request, token);

            if (result.IsSuccess)
            {
                return await SucceededAsync(services, effect, token);
            }

            return result.Error.IsOutcomeUnknown
                ? new WriteOutcomeUnknown(effect.OperationId, _clock.GetUtcNow(), result.Error)
                : new WriteFailed(effect.OperationId, _clock.GetUtcNow(), result.Error);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Something broke after the request may have gone out: never claim "not done".
            var error = new WriteError(
                WriteErrorKind.UnknownOutcome,
                null,
                $"{UnexpectedMessage} ({exception.GetType().Name}).",
                [],
                []);

            return new WriteOutcomeUnknown(effect.OperationId, _clock.GetUtcNow(), error);
        }
    }

    /// <summary>
    /// The create call. On success the new issue is read by key for its row (best effort: the search index may not
    /// show it yet, the key still arrives). A refusal is a definite failure; a lost reply is never repeated.
    /// </summary>
    private async Task<Command> CreateAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        CreateRequest create,
        CancellationToken token)
    {
        NewIssue issue;

        try
        {
            issue = NewIssues.For(create);
        }
        catch (Exception exception) when (exception is FormatException or NotSupportedException)
        {
            // Nothing was sent, so this is a definite failure and not a lost reply.
            var error = new WriteError(WriteErrorKind.Validation, null, exception.Message, [], []);

            return new WriteFailed(effect.OperationId, _clock.GetUtcNow(), error);
        }

        // Remembered so the check of a lost reply only accepts issues created from now on, not an identical older twin.
        _createSubmitted[effect.OperationId] = _clock.GetUtcNow();
        var result = await services.Creates.CreateIssueAsync(issue, token);

        if (!result.IsSuccess)
        {
            if (!result.Error.IsOutcomeUnknown)
            {
                _createSubmitted.TryRemove(effect.OperationId, out _);
            }

            return result.Error.IsOutcomeUnknown
                ? new WriteOutcomeUnknown(effect.OperationId, _clock.GetUtcNow(), result.Error)
                : new WriteFailed(effect.OperationId, _clock.GetUtcNow(), result.Error);
        }

        _createSubmitted.TryRemove(effect.OperationId, out _);

        var key = result.Value.Key;
        var rows = await ReadKeysAsync(services, [key], [], token);
        var shown = rows is null ? [] : rows.Where(row => row.Key == key).ToArray();

        return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), shown, CreatedKey: key);
    }

    /// <summary>
    /// A lost reply of a create: the recent issues of the account in the project are searched for the same type and
    /// summary. Found means it was created; not found proves nothing (the index lags), so the outcome stays unknown.
    /// </summary>
    private async Task<Command?> VerifyCreateAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        CreateRequest create,
        CancellationToken token)
    {
        try
        {
            // Only issues created since the submit count (minutes rounded up, relative to the server's now,
            // so no time zone is involved); without a remembered submit time the old ten minutes apply.
            var recent = _createSubmitted.TryGetValue(effect.OperationId, out var submitted)
                ? JqlBuilder.OwnIssuesSince((int)Math.Ceiling((_clock.GetUtcNow() - submitted).TotalMinutes))
                : JqlBuilder.RecentOwnIssues();
            var jql = JqlBuilder.WithOrder(
                JqlBuilder.And(JqlBuilder.Project(create.ProjectKey), recent),
                JqlOrder.CreatedDescending);
            var result = await services.Search.SearchAsync(new IssueSearchQuery(jql), null, token);
            var found = result.IsSuccess ? result.Value.Items.Where(create.IsShownBy).Take(1).ToArray() : [];
            var outcome = found.Length > 0 ? VerifyOutcome.Applied : VerifyOutcome.Unreachable;

            if (outcome == VerifyOutcome.Applied)
            {
                _createSubmitted.TryRemove(effect.OperationId, out _);
            }

            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, found);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), VerifyOutcome.Unreachable, []);
        }
    }

    /// <summary>
    /// A comment write: no row is read afterwards (it changes none); the reply carries the stored comment instead.
    /// </summary>
    private async Task<Command> ExecuteCommentAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        CommentRequest request,
        CancellationToken token)
    {
        var reply = await CommentWriter.SendAsync(services, request, token);

        if (reply.Error is not { } error)
        {
            return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), [], Comment: reply.Comment);
        }

        return error.IsOutcomeUnknown
            ? new WriteOutcomeUnknown(effect.OperationId, _clock.GetUtcNow(), error)
            : new WriteFailed(effect.OperationId, _clock.GetUtcNow(), error);
    }

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

        if (request is RankBlockRequest block)
        {
            return await RankBlockAsync(services, block, token);
        }

        if (request is RankRequest rank)
        {
            return await RankAsync(services, rank, token);
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

    /// <summary>
    /// The write was accepted: reads the issues again (one search) for the server rows. The read is best effort: a
    /// failed one, or one that still shows the old value, leaves the rows to the next reload.
    /// </summary>
    private async Task<Command> SucceededAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        CancellationToken token)
    {
        if (effect.Request.RemovesIssues || !effect.Request.ReadsBack)
        {
            return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), []);
        }

        var rows = await ReadKeysAsync(services, effect.Request.Keys, effect.Request.ReadBackFieldIds, token);
        var shown = rows is null ? [] : rows.Where(effect.Request.IsShownBy).ToArray();

        return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), shown);
    }

    private async Task<Command?> VerifyAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        CancellationToken token)
    {
        if (effect.Request is CommentRequest comment)
        {
            var check = await CommentWriter.VerifyAsync(services, comment, token);

            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), check.Outcome, [], check.Comment);
        }

        if (effect.Request is DeleteIssueRequest delete)
        {
            return await VerifyDeleteAsync(services, effect, delete, token);
        }

        if (effect.Request is CreateRequest create)
        {
            return await VerifyCreateAsync(services, effect, create, token);
        }

        if (effect.Request is LinkWriteRequest linkRequest)
        {
            return await VerifyLinkAsync(services, effect, linkRequest, token);
        }

        if (!effect.Request.ReadsBack)
        {
            // Nothing a read of the issue shows can tell (a rank): the user is told to reload.
            return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), VerifyOutcome.Unreachable, []);
        }

        var rows = await ReadKeysAsync(services, effect.Request.Keys, effect.Request.ReadBackFieldIds, token);
        var outcome = OutcomeOf(effect.Request, rows);

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, rows ?? []);
    }

    /// <summary>
    /// A lost reply of a delete: the issue is read by key (a read that does not lag like the search index). Gone
    /// means the delete happened, readable means it did not, anything else is unknown.
    /// </summary>
    private async Task<Command?> VerifyDeleteAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        DeleteIssueRequest delete,
        CancellationToken token)
    {
        var outcome = VerifyOutcome.Unreachable;

        try
        {
            var result = await services.Issues.GetIssueAsync(delete.Key, token);

            if (result.IsSuccess)
            {
                outcome = VerifyOutcome.NotApplied;
            }
            else if (result.Error.Kind == JiraErrorKind.NotFound)
            {
                outcome = VerifyOutcome.Applied;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
        }

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, []);
    }

    /// <summary>
    /// A lost reply of a link write: the issue is read by key (links are part of it, and the read does not lag like
    /// the search index). The issue shows the change or it does not; a read that fails decides nothing.
    /// </summary>
    private async Task<Command?> VerifyLinkAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        LinkWriteRequest request,
        CancellationToken token)
    {
        var outcome = VerifyOutcome.Unreachable;

        try
        {
            var result = await services.Issues.GetIssueAsync(request.Key, token);

            if (result.IsSuccess)
            {
                outcome = request.IsShownBy(result.Value) ? VerifyOutcome.Applied : VerifyOutcome.NotApplied;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
        }

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, []);
    }

    private static VerifyOutcome OutcomeOf(WriteRequest request, IReadOnlyList<IssueSummary>? rows)
    {
        if (rows is null || rows.Count < request.Keys.Count)
        {
            return VerifyOutcome.Unreachable;
        }

        // The read is a search, and the search index lags behind a write: an old value proves nothing, so only a
        // row that shows the change decides. "Not applied" needs a read that cannot lag (none is used yet).
        return rows.All(request.IsShownBy) ? VerifyOutcome.Applied : VerifyOutcome.Unreachable;
    }

    private static async Task<IReadOnlyList<IssueSummary>?> ReadKeysAsync(
        JiraServices services,
        IReadOnlyList<string> keys,
        IReadOnlyList<string> extraFieldIds,
        CancellationToken token)
    {
        try
        {
            var query = new IssueSearchQuery(JqlBuilder.KeyIn(keys), keys.Count)
            {
                Extras = new IssueExtraFields(null, extraFieldIds),
            };
            var result = await services.Search.SearchAsync(query, null, token);

            return result.IsSuccess ? result.Value.Items : null;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
            return null;
        }
    }

    private static WriteError NotSent()
    {
        return new WriteError(WriteErrorKind.NotSent, null, NoConnectionMessage, [], []);
    }
}
