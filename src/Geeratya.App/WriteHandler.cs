using System.Collections.Concurrent;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Core.Writes.Comments;
using Geeratya.Jira.Create.Models;
using Geeratya.Jira.Jql;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.App;

/// <summary>
/// Turns <see cref="ExecuteWriteEffect"/> and <see cref="VerifyWriteEffect"/> into the commands that report them.
/// A write runs on the <see cref="NetworkRunner"/> detached from any tab, so closing the tab that started it never
/// cancels it (a sent write cannot be undone, and cancelling would only hide the answer); a profile switch or exit
/// does, and a reply of an older site is dropped before it is posted. A write is never repeated: a
/// lost reply becomes <see cref="WriteOutcomeUnknown"/> and the issue is read to find out.
/// </summary>
public sealed partial class WriteHandler
{
    private const string NoConnectionMessage = "Not connected to Jira: check the profile and its API token (:profile).";

    private const int MaxKeysNamed = 8;

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

            if (effect.Request is SprintWriteRequest sprint)
            {
                return await ExecuteSprintAsync(services, effect, sprint, token);
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
}
