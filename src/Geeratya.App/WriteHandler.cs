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

    public WriteHandler(NetworkRunner runner, TimeProvider? clock = null)
    {
        _runner = runner;
        _clock = clock ?? TimeProvider.System;
    }

    /// <summary>Starts the effect and returns the task that ends when its answer is posted or dropped.</summary>
    public Task StartAsync(Effect effect)
    {
        return effect switch
        {
            ExecuteWriteEffect execute => StartExecute(execute),
            VerifyWriteEffect verify => StartVerify(verify),
            _ => Task.CompletedTask,
        };
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

        throw new NotSupportedException($"No handler for {request.GetType().Name}.");
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
