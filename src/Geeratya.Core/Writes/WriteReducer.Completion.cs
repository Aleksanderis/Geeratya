using Geeratya.Core.Detail;
using Geeratya.Core.Links;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Writes;

public static partial class WriteReducer
{
    private static ReduceResult PhaseChanged(AppState state, OperationPhaseChanged changed)
    {
        var operation = Find(state, changed.OperationId);

        return operation is null
            ? new ReduceResult(state, _noEffects)
            : new ReduceResult(ReplaceOperation(state, operation with { Phase = changed.Phase }), _noEffects);
    }

    private static ReduceResult Succeeded(AppState state, WriteSucceeded succeeded)
    {
        var operation = Find(state, succeeded.OperationId);

        return operation is null
            ? new ReduceResult(state, _noEffects)
            : Warned(Complete(state, operation, succeeded.Rows, succeeded.CreatedKey), succeeded.Warning);
    }

    private static ReduceResult Warned(ReduceResult result, string? warning)
    {
        return warning is null ? result : result with { State = StatusMessages.Warn(result.State, warning) };
    }

    private static ReduceResult Verified(AppState state, WriteVerified verified)
    {
        var operation = Find(state, verified.OperationId);

        if (operation is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        return verified.Outcome switch
        {
            VerifyOutcome.Applied => WithMessage(
                Complete(state, operation, verified.Rows, FirstKey(verified.Rows)),
                operation.Request),
            VerifyOutcome.NotApplied => RolledBack(state, operation, operation.Request.NotAppliedText, []),
            _ => Unreachable(state, operation),
        };
    }

    private static ReduceResult WithMessage(ReduceResult result, WriteRequest request)
    {
        if (result.State.Message != request.DoneText)
        {
            return result;
        }

        var text = request.VerifiedDoneText;

        // A completed sprint whose issues were not moved needs the user to act: a warning, not a plain line.
        var state = request is CompleteSprintRequest { OpenKeys.Count: > 0 }
            ? StatusMessages.Warn(result.State, text)
            : result.State with { Message = text };

        return result with { State = state };
    }

    /// <summary>The check itself failed: nothing is known, so the rows go back and are marked stale.</summary>
    private static ReduceResult Unreachable(AppState state, Operation operation)
    {
        var text = operation.Request.UnreachableText;
        var marked = operation.Request.ChangesRows ? MarkStale(state, operation.Keys) : state;

        return RolledBack(marked, operation, text, [new InvalidateCacheEffect(operation.Keys)], true);
    }

    /// <summary>Jira confirmed the write: the server rows replace the tabs' rows, the caches are dropped.</summary>
    private static ReduceResult Complete(
        AppState state,
        Operation operation,
        IReadOnlyList<IssueSummary> rows,
        string? createdKey)
    {
        if (operation.Request.RemovesIssues)
        {
            return CompleteRemoval(state, operation);
        }

        if (operation.Request is CreateRequest create)
        {
            return CompleteCreation(state, operation, create, createdKey, rows);
        }

        if (operation.Request is LinkWriteRequest)
        {
            return CompleteLink(state, operation);
        }

        if (operation.Request is SprintWriteRequest sprint)
        {
            return CompleteSprint(state, operation, sprint);
        }

        var effects = new List<Effect>();

        if (operation.Request.ChangesRows)
        {
            effects.Add(new InvalidateCacheEffect(operation.Keys));
        }

        var replaced = ReplaceRows(state, rows);
        var stale = operation.Request.ChangesRows ? MarkStale(replaced, operation.Keys) : replaced;
        var queued = ClearSelection(QueueRankReload(stale, operation), operation);
        var finished = Finish(queued, operation, true, effects, rows);
        var reloaded = operation.Request.ChangesRows ? ReloadDetails(finished, operation.Keys, effects) : finished;

        return new ReduceResult(reloaded with { Message = operation.Request.DoneText }, effects);
    }

    /// <summary>
    /// Jira deleted the issues: their rows and open details go from every tab, the caches are dropped, and the
    /// origin tab reloads (membership changed; the server is the truth, <c>docs/internal/architecture.md</c>).
    /// </summary>
    private static ReduceResult CompleteRemoval(AppState state, Operation operation)
    {
        var effects = new List<Effect> { new InvalidateCacheEffect(operation.Keys) };
        var removed = WriteRemoval.Apply(state, operation.Keys);
        var finished = Finish(removed, operation, false, effects);
        var origin = ViewReducer.Find(finished.Session.Tabs, operation.Origin);
        var reloaded = origin is { Cold: false, Load: not Loading }
            ? ViewReducer.StartRefresh(finished, origin, false, effects)
            : finished;

        return new ReduceResult(reloaded with { Message = operation.Request.DoneText }, effects);
    }

    /// <summary>
    /// Jira created the issue: the caches are dropped and the origin tab reloads so the issue appears where it
    /// belongs (membership changed; the server is the truth). A form opened on the parent's detail reloads that
    /// detail (its sub-tasks or children changed). With <c>OpenAfter</c> the origin tab opens the new issue.
    /// </summary>
    private static ReduceResult CompleteCreation(
        AppState state,
        Operation operation,
        CreateRequest create,
        string? key,
        IReadOnlyList<IssueSummary> rows)
    {
        var keys = key is null ? [] : new[] { key };
        var effects = new List<Effect> { new InvalidateCacheEffect(keys) };
        var finished = Finish(state, operation, false, effects);
        var origin = ViewReducer.Find(finished.Session.Tabs, operation.Origin);
        var reloaded = origin is { Cold: false, Load: not Loading }
            ? ViewReducer.StartRefresh(finished, origin, false, effects)
            : finished;
        var parents = create.ParentKey is null ? [] : new[] { create.ParentKey };
        var details = ReloadDetails(reloaded, parents, effects);
        var opened = key is not null && create.OpenAfter && origin is not null
            ? DetailReducer.Open(details, origin.Id, key, (rows.Count > 0 ? rows[0] : null), effects)
            : details;

        var message = key is null ? create.DoneText : CreateRequest.CreatedText(key);

        return new ReduceResult(opened with { Message = message }, effects);
    }

    private static string? FirstKey(IReadOnlyList<IssueSummary> rows)
    {
        return rows.Count > 0 ? rows[0].Key : null;
    }

    /// <summary>
    /// Jira changed a link: no row shows links, so no row is replaced and no tab goes stale; the caches of both
    /// issues are dropped and every open detail of either reloads.
    /// </summary>
    private static ReduceResult CompleteLink(AppState state, Operation operation)
    {
        var effects = new List<Effect> { new InvalidateCacheEffect(operation.Keys) };
        var finished = Finish(state, operation, false, effects);
        var reloaded = ReloadDetails(finished, operation.Keys, effects);

        return new ReduceResult(reloaded with { Message = operation.Request.DoneText }, effects);
    }

    /// <summary>
    /// Jira changed a sprint: no row shows it, so nothing is patched; every tab of the board that shows sprints
    /// (Scrum backlog, Sprint view) reloads to pick up the new header and the new section.
    /// </summary>
    private static ReduceResult CompleteSprint(AppState state, Operation operation, SprintWriteRequest request)
    {
        var effects = new List<Effect>();

        if (operation.Keys.Count > 0)
        {
            // A completed sprint moved its open issues: what the caches hold of them is old.
            effects.Add(new InvalidateCacheEffect(operation.Keys));
        }

        var next = Finish(state, operation, false, effects);

        foreach (var tab in next.Session.Tabs.Where(candidate => ShowsSprintsOf(candidate, request.BoardId)).ToArray())
        {
            var current = ViewReducer.Find(next.Session.Tabs, tab.Id);

            if (current is { Cold: false, Load: not Loading })
            {
                next = ViewReducer.StartRefresh(next, current, false, effects);
            }
        }

        return new ReduceResult(next with { Message = operation.Request.DoneText }, effects);
    }

    private static bool ShowsSprintsOf(TabState tab, long boardId)
    {
        return tab.Definition.Source is ViewSourceKind.ScrumBacklog or ViewSourceKind.Sprint
            && SprintBoards.BoardOf(tab.Definition) == boardId;
    }

    private static ReduceResult Failed(AppState state, WriteFailed failed)
    {
        if (failed.Error.IsOutcomeUnknown)
        {
            return Unknown(state, failed.OperationId);
        }

        var operation = Find(state, failed.OperationId);

        if (operation is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        var text = WriteFailureText.Of(failed.Error, operation.Request);
        var effects = new List<Effect>();

        // A sprint write fails about the sprint, not about the issues it would have moved.
        var staleKeys = operation.Request is SprintWriteRequest ? [] : operation.Keys;

        if (failed.Error.Kind == WriteErrorKind.NotFound && staleKeys.Count > 0)
        {
            effects.Add(new InvalidateCacheEffect(staleKeys));
        }

        var rejected = failed.Error.Kind == WriteErrorKind.Unauthorized;
        var linked = rejected ? state with { Session = state.Session with { Link = new Rejected() } } : state;
        var marked = failed.Error.Kind == WriteErrorKind.NotFound ? MarkStale(linked, staleKeys) : linked;

        var partial = KeepFailedMarks(marked, operation, failed.Error);

        return RolledBack(partial, operation, text, effects, ReloadsAfter(failed.Error) || operation.Kind == OperationKind.RankMove || !ReferenceEquals(partial, marked));
    }

    /// <summary>
    /// A sprint move that took some issues: the moved ones leave the marks and the Visual range, the ones that were
    /// not moved stay marked so the move can be tried again.
    /// </summary>
    private static AppState KeepFailedMarks(AppState state, Operation operation, WriteError error)
    {
        if (operation.Kind is not (OperationKind.SprintMove or OperationKind.RankMove) ||
            error.FailedKeys is not { } failed)
        {
            return state;
        }

        var failedSet = KeySet(failed);
        var moved = operation.Keys.Where(key => !failedSet.Contains(key)).ToArray();

        if (moved.Length == 0)
        {
            return state;
        }

        var movedSet = KeySet(moved);
        var tabs = state.Session.Tabs
            .Select(tab => Holds(tab, movedSet) ? Unselected(tab, movedSet) : tab)
            .ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    /// <summary>Undoes the optimistic rows where they still hold the patched value and ends the operation.</summary>
    private static ReduceResult RolledBack(
        AppState state,
        Operation operation,
        string text,
        List<Effect> effects,
        bool reload = false)
    {
        var reverted = MapChanges(state, operation.Undo, true);

        foreach (var change in operation.Undo)
        {
            reverted = Reorder(reverted, change, true);
        }

        reverted = FollowCard(reverted, operation.Request, operation.Origin);

        var queued = reload ? QueueRankReload(reverted, operation) : reverted;
        var finished = Finish(queued, operation, false, effects);

        // A detail reply that landed during the write may hold the optimistic value: read the issue again.
        var reloaded = operation.Request.ChangesRows ? ReloadDetails(finished, operation.Keys, effects) : finished;

        return new ReduceResult(StatusMessages.Fail(reloaded, text), effects);
    }

    /// <summary>The reply was lost: keep the optimistic rows and read the issue to find out what happened.</summary>
    private static ReduceResult Unknown(AppState state, long operationId)
    {
        var operation = Find(state, operationId);

        if (operation is null || operation.Phase == OperationPhase.Verifying)
        {
            return new ReduceResult(state, _noEffects);
        }

        var verifying = ReplaceOperation(state, operation with { Phase = OperationPhase.Verifying });

        // The issue may have changed, so the caches are dropped now, not only when the check ends.
        return new ReduceResult(
            verifying with { Message = operation.Request.CheckingText },
            [new VerifyWriteEffect(operation.Id, operation.Request), new InvalidateCacheEffect(operation.Keys)]);
    }
}
