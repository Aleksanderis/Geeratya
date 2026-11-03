using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Writes;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Writes;

namespace Geeratya.App;

public sealed partial class WriteHandler
{
    /// <summary>A sprint write: no issue is read afterwards, the tabs of the board reload when it succeeded.</summary>
    private async Task<Command> ExecuteSprintAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        SprintWriteRequest request,
        CancellationToken token)
    {
        if (request is CompleteSprintRequest complete)
        {
            return await CompleteSprintAsync(services, effect, complete, token);
        }

        var result = request switch
        {
            CreateSprintRequest create => await services.SprintWrites.CreateAsync(
                create.Name,
                create.BoardId,
                create.Goal,
                create.StartDate,
                create.EndDate,
                token),
            EditSprintRequest { Edit.ClearsDate: true } clear
                => await services.SprintWrites.ReplaceAsync(clear.Before, clear.Edit, token),
            EditSprintRequest edit => await services.SprintWrites.UpdateAsync(edit.Before.Id, edit.Edit, token),
            StartSprintRequest start => await services.SprintWrites.StartAsync(
                start.Sprint.Id,
                start.Start,
                start.End,
                token),
            _ => throw new NotSupportedException($"No handler for {request.GetType().Name}."),
        };

        if (result.IsSuccess)
        {
            return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), []);
        }

        return result.Error.IsOutcomeUnknown
            ? new WriteOutcomeUnknown(effect.OperationId, _clock.GetUtcNow(), result.Error)
            : new WriteFailed(effect.OperationId, _clock.GetUtcNow(), result.Error);
    }

    /// <summary>
    /// A lost reply of a sprint edit: the sprint is read by id and compared. A create has no id to read, so it stays
    /// unknown and the status line tells the user to look before creating it again.
    /// </summary>
    private async Task<Command?> VerifySprintAsync(
        JiraServices services,
        VerifyWriteEffect effect,
        SprintWriteRequest request,
        CancellationToken token)
    {
        var outcome = VerifyOutcome.Unreachable;

        try
        {
            if (SprintOf(request) is { } id)
            {
                var result = await services.Sprints.GetSprintAsync(id, token);

                if (result.IsSuccess)
                {
                    outcome = IsShown(request, result.Value) ? VerifyOutcome.Applied : VerifyOutcome.NotApplied;
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A read that breaks is a read that failed: it never changes what is known about the write.
        }

        return new WriteVerified(effect.OperationId, _clock.GetUtcNow(), outcome, []);
    }

    /// <summary>
    /// Complete is two calls: the sprint is completed, then its open issues are moved (Jira's complete does not move
    /// them). A refusal or lost reply of the first is the outcome of the write; when only the move goes wrong the
    /// sprint is closed, so the write counts as done with a warning that says which issues were not moved.
    /// </summary>
    private async Task<Command> CompleteSprintAsync(
        JiraServices services,
        ExecuteWriteEffect effect,
        CompleteSprintRequest request,
        CancellationToken token)
    {
        var result = await services.SprintWrites.CompleteAsync(request.Sprint.Id, token);

        if (!result.IsSuccess)
        {
            return result.Error.IsOutcomeUnknown
                ? new WriteOutcomeUnknown(effect.OperationId, _clock.GetUtcNow(), result.Error)
                : new WriteFailed(effect.OperationId, _clock.GetUtcNow(), result.Error);
        }

        if (request.OpenKeys.Count == 0)
        {
            return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), []);
        }

        var moved = await MoveOpenIssuesAsync(services, request, token);

        return new WriteSucceeded(effect.OperationId, _clock.GetUtcNow(), [], Warning: MoveWarning(request, moved));
    }

    private static Task<WriteResult<SprintMoveOutcome>> MoveOpenIssuesAsync(
        JiraServices services,
        CompleteSprintRequest request,
        CancellationToken token)
    {
        if (request.MoveTo is { } next)
        {
            return services.SprintMoves.MoveToSprintAsync(next.Id, request.OpenKeys, token);
        }

        return services.SprintMoves.MoveToBacklogAsync(request.OpenKeys, token);
    }

    private static string? MoveWarning(CompleteSprintRequest request, WriteResult<SprintMoveOutcome> moved)
    {
        var total = request.OpenKeys.Count;
        var name = request.Sprint.Name;

        if (!moved.IsSuccess)
        {
            var all = SprintTally.Issues(total);

            return $"Completed \"{name}\", but its {all} were not moved to {request.Destination}: "
                + $"{moved.Error.Message}. They stay in the closed sprint.";
        }

        var outcome = moved.Value;

        if (outcome.AllMoved)
        {
            return null;
        }

        var left = total - outcome.Moved.Count;
        var lost = outcome.Unknown.Count > 0 ? " (some replies were lost: reload to see them)" : string.Empty;
        var reason = outcome.Error?.Message ?? "Jira refused them";

        return $"Completed \"{name}\", but {left} of {total} open issues were not moved to {request.Destination}"
            + $"{lost}: {reason}. They stay in the closed sprint.";
    }

    private static long? SprintOf(SprintWriteRequest request)
    {
        return request switch
        {
            EditSprintRequest edit => edit.Before.Id,
            StartSprintRequest start => start.Sprint.Id,
            CompleteSprintRequest complete => complete.Sprint.Id,
            _ => null,
        };
    }

    private static bool IsShown(SprintWriteRequest request, Sprint sprint)
    {
        return request switch
        {
            EditSprintRequest edit => edit.IsShownBy(sprint),
            StartSprintRequest => StartSprintRequest.IsShownBy(sprint),
            CompleteSprintRequest => CompleteSprintRequest.IsShownBy(sprint),
            _ => false,
        };
    }
}
