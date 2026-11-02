using System.Text.Json;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Jira.Edit.Models;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search.Models;
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
        var rows = await ReadAsync(services, effect.Request, token);
        var shown = rows is null ? [] : rows.Where(effect.Request.IsShownBy).ToArray();

        return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), shown);
    }

    private async Task<Command?> VerifyAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        CancellationToken token)
    {
        var rows = await ReadAsync(services, effect.Request, token);
        var outcome = OutcomeOf(effect.Request, rows);

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, rows ?? []);
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

    private static async Task<IReadOnlyList<IssueSummary>?> ReadAsync(
        JiraServices services,
        WriteRequest request,
        CancellationToken token)
    {
        try
        {
            var query = new IssueSearchQuery(JqlBuilder.KeyIn(request.Keys), request.Keys.Count);
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
