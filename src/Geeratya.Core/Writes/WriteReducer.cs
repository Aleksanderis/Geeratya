using System.Globalization;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Links;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The write part of the reducer (<c>docs/architecture.md</c>, "Write pipeline"): starts operations, refuses a write on
/// an issue that is being written, patches the rows optimistically in every tab, and settles the operation from the
/// reply (success, definite failure with rollback, lost reply with verification). Pure: no clock, no I/O.
/// </summary>
public static class WriteReducer
{
    private const string NotConnectedText = "Not connected to Jira: cannot tell which account is yours.";
    private const string NoProfileText = "No profile is active.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Reduce(AppState state, WriteCommand command)
    {
        return command switch
        {
            AssignToMeRequested me => AssignToMe(state, me),
            WriteRequested requested => Request(state, requested),
            SubtasksCounted counted => DeleteReducer.Counted(state, counted),
            SubtasksCountFailed failed => DeleteReducer.CountFailed(state, failed),
            OperationPhaseChanged changed => PhaseChanged(state, changed),
            WriteSucceeded succeeded => FormReducer.Settled(
                Succeeded(state, succeeded),
                succeeded.OperationId,
                true,
                null),
            WriteFailed failed => FormReducer.Settled(Failed(state, failed), failed.OperationId, false, failed.Error),
            WriteOutcomeUnknown unknown => Unknown(state, unknown.OperationId),
            WriteVerified verified => FormReducer.Settled(
                Verified(state, verified),
                verified.OperationId,
                verified.Outcome == VerifyOutcome.Applied,
                null),
            _ => new ReduceResult(state, _noEffects),
        };
    }

    /// <summary>
    /// The warning for quitting or switching profile while writes run, or null when none does. Their results are
    /// dropped by the generation check, so the user is asked first.
    /// </summary>
    public static string? RunningText(AppState state)
    {
        var count = state.Session.Operations.Count;

        if (count == 0)
        {
            return null;
        }

        if (count == 1)
        {
            return "1 write is still running; its result will not be shown";
        }

        var number = count.ToString(CultureInfo.InvariantCulture);

        return $"{number} writes are still running; their results will not be shown";
    }

    private static ReduceResult AssignToMe(AppState state, AssignToMeRequested request)
    {
        if (state.Session.Connection is not Connected { AccountId: { } accountId } connected)
        {
            return Say(state, NotConnectedText);
        }

        var assignee = new IssueAssignee(accountId, connected.DisplayName);

        return Request(state, new WriteRequested(request.Tab, new AssignRequest(request.Key, assignee), request.At));
    }

    private static ReduceResult Request(AppState state, WriteRequested requested)
    {
        var session = state.Session;
        var write = requested.Request;

        if (session.ProfileName is null)
        {
            return Say(state, NoProfileText);
        }

        if (BusyText(session.Operations, write) is { } busy)
        {
            return Say(state, busy);
        }

        var id = state.Sequence + 1;
        var changes = Plan(session.Tabs, write);
        var operation = new Operation(id, write, requested.Tab, OperationPhase.Sending, changes, requested.At);
        var patched = Patch(state, changes);

        var next = patched with
        {
            Sequence = id,
            Message = write.StartedText,
            Session = patched.Session with { Operations = [.. session.Operations, operation] },
        };

        return new ReduceResult(next, [new ExecuteWriteEffect(id, write)]);
    }

    private static string? BusyText(IReadOnlyList<Operation> operations, WriteRequest write)
    {
        foreach (var operation in operations)
        {
            foreach (var key in write.Keys)
            {
                if (operation.Keys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    return $"{key} is busy ({operation.Request.BusyText})";
                }
            }
        }

        return null;
    }

    /// <summary>The optimistic change per key, taken from the first tab that shows the issue.</summary>
    private static List<RowChange> Plan(IReadOnlyList<TabState> tabs, WriteRequest write)
    {
        var changes = new List<RowChange>();

        foreach (var key in write.Keys)
        {
            var row = FindRow(tabs, key);
            var change = row is null ? null : write.Plan(row);

            if (change is not null)
            {
                changes.Add(change);
            }
        }

        return changes;
    }

    private static IssueSummary? FindRow(IReadOnlyList<TabState> tabs, string key)
    {
        foreach (var tab in tabs)
        {
            foreach (var row in tab.Rows)
            {
                if (string.Equals(row.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }
        }

        return null;
    }

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

        return operation is null ? new ReduceResult(state, _noEffects) : Complete(state, operation, succeeded.Rows);
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
            VerifyOutcome.Applied => Complete(state, operation, verified.Rows),
            VerifyOutcome.NotApplied => RolledBack(state, operation, operation.Request.NotAppliedText, []),
            _ => Unreachable(state, operation),
        };
    }

    /// <summary>The check itself failed: nothing is known, so the rows go back and are marked stale.</summary>
    private static ReduceResult Unreachable(AppState state, Operation operation)
    {
        var keys = string.Join(", ", operation.Keys);
        var text = $"{keys}: could not check the result; reload to see the current state";
        var marked = MarkStale(state, operation.Keys);

        return RolledBack(marked, operation, text, [new InvalidateCacheEffect(operation.Keys)]);
    }

    /// <summary>Jira confirmed the write: the server rows replace the tabs' rows, the caches are dropped.</summary>
    private static ReduceResult Complete(AppState state, Operation operation, IReadOnlyList<IssueSummary> rows)
    {
        if (operation.Request.RemovesIssues)
        {
            return CompleteRemoval(state, operation);
        }

        if (operation.Request is LinkWriteRequest)
        {
            return CompleteLink(state, operation);
        }

        var effects = new List<Effect> { new InvalidateCacheEffect(operation.Keys) };
        var replaced = ReplaceRows(state, rows);
        var stale = MarkStale(replaced, operation.Keys);
        var finished = Finish(stale, operation, true, effects, rows);
        var reloaded = ReloadDetails(finished, operation.Keys, effects);

        return new ReduceResult(reloaded with { Message = operation.Request.DoneText }, effects);
    }

    /// <summary>
    /// Jira deleted the issues: their rows and open details go from every tab, the caches are dropped, and the
    /// origin tab reloads (membership changed; the server is the truth, <c>docs/architecture.md</c>).
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

        if (failed.Error.Kind == WriteErrorKind.NotFound)
        {
            effects.Add(new InvalidateCacheEffect(operation.Keys));
        }

        var rejected = failed.Error.Kind == WriteErrorKind.Unauthorized;
        var linked = rejected ? state with { Session = state.Session with { Link = new Rejected() } } : state;
        var marked = failed.Error.Kind == WriteErrorKind.NotFound ? MarkStale(linked, operation.Keys) : linked;

        return RolledBack(marked, operation, text, effects);
    }

    /// <summary>Undoes the optimistic rows where they still hold the patched value and ends the operation.</summary>
    private static ReduceResult RolledBack(
        AppState state,
        Operation operation,
        string text,
        List<Effect> effects)
    {
        var reverted = state;

        foreach (var change in operation.Undo)
        {
            reverted = MapRows(reverted, [change.Key], change.Revert);
        }

        var finished = Finish(reverted, operation, false, effects);

        // A detail reply that landed during the write may hold the optimistic value: read the issue again.
        var reloaded = ReloadDetails(finished, operation.Keys, effects);

        return new ReduceResult(reloaded with { Message = text }, effects);
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

    /// <summary>
    /// Removes the operation. An applied write protects the rows of its keys in loads that are still in flight (they
    /// may have started before it ended); when the last operation ends, refreshes that were asked for meanwhile run.
    /// </summary>
    private static AppState Finish(
        AppState state,
        Operation operation,
        bool applied,
        List<Effect> effects,
        IReadOnlyList<IssueSummary>? rows = null)
    {
        var remaining = state.Session.Operations.Where(other => other.Id != operation.Id).ToArray();
        var removed = state with { Session = state.Session with { Operations = remaining } };
        var protectedState = applied ? Protect(removed, operation, rows ?? []) : removed;

        return remaining.Length == 0 ? RunQueuedRefreshes(protectedState, effects) : protectedState;
    }

    private static AppState Protect(AppState state, Operation operation, IReadOnlyList<IssueSummary> rows)
    {
        var tabs = state.Session.Tabs.Select(tab => ProtectedTab(tab, operation, rows)).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    /// <summary>
    /// A tab that is loading keeps its rows for the written keys; an issue detail of those keys that is loading keeps
    /// the fields the write changed (the reply may predate the write).
    /// </summary>
    private static TabState ProtectedTab(TabState tab, Operation operation, IReadOnlyList<IssueSummary> rows)
    {
        var details = tab.Detail.Select(detail => HeldDetail(detail, operation)).ToArray();
        var held = tab with { Detail = details };

        return tab.Load is Loading ? Protected(held, operation.Keys, rows) : held;
    }

    private static DetailState HeldDetail(DetailState detail, Operation operation)
    {
        if (detail.IssueRequest == 0 || !operation.Keys.Contains(detail.Key, StringComparer.OrdinalIgnoreCase))
        {
            return detail;
        }

        var changes = operation.Undo.Where(
            change => string.Equals(change.Key, detail.Key, StringComparison.OrdinalIgnoreCase));

        return detail with { HeldChanges = [.. detail.HeldChanges ?? [], .. changes] };
    }

    private static TabState Protected(TabState tab, IReadOnlyList<string> keys, IReadOnlyList<IssueSummary> rows)
    {
        return tab with
        {
            ProtectedKeys = [.. tab.ProtectedKeys.Union(keys, StringComparer.OrdinalIgnoreCase)],
            ProtectedRows = [.. tab.ProtectedRows.Where(row => !IsAmong(keys, row.Key)), .. rows],
        };
    }

    /// <summary>
    /// The shown issue of every tab that holds one of the keys is marked stale; the active tab reloads it now, the
    /// others when they are activated.
    /// </summary>
    private static AppState ReloadDetails(AppState state, IReadOnlyList<string> keys, List<Effect> effects)
    {
        var next = state;

        foreach (var tab in state.Session.Tabs)
        {
            if (tab.Detail is not [.., { Issue: not null } top] || !IsAmong(keys, top.Key))
            {
                continue;
            }

            var current = ViewReducer.Find(next.Session.Tabs, tab.Id)!;
            var stack = current.Detail.ToArray();

            stack[^1] = top with { IssueStale = true };

            var marked = current with { Detail = stack };
            var staleState = ViewReducer.Replace(next, marked);

            next = tab.Id == state.Session.Active ? DetailReducer.Reload(staleState, tab.Id, effects) : staleState;
        }

        return next;
    }

    private static bool IsAmong(IReadOnlyList<string> keys, string key)
    {
        return keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }

    private static AppState RunQueuedRefreshes(AppState state, List<Effect> effects)
    {
        var next = state;

        foreach (var queued in state.Session.Tabs.Where(tab => tab.RefreshQueued))
        {
            var current = ViewReducer.Find(next.Session.Tabs, queued.Id)!;
            var cleared = current with { RefreshQueued = false };

            next = ViewReducer.Replace(next, cleared);

            if (!cleared.Cold && cleared.Load is not Loading)
            {
                next = ViewReducer.StartRefresh(next, cleared, false, effects);
            }
        }

        return next;
    }

    private static AppState Patch(AppState state, IReadOnlyList<RowChange> changes)
    {
        var patched = state;

        foreach (var change in changes)
        {
            patched = MapRows(patched, [change.Key], change.Patch);
        }

        return patched;
    }

    private static AppState ReplaceRows(AppState state, IReadOnlyList<IssueSummary> fresh)
    {
        if (fresh.Count == 0)
        {
            return state;
        }

        var byKey = fresh.ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);

        return MapRows(state, [.. byKey.Keys], row => byKey.GetValueOrDefault(row.Key, row));
    }

    private static AppState MarkStale(AppState state, IReadOnlyList<string> keys)
    {
        var tabs = state.Session.Tabs.Select(tab => Holds(tab, keys) ? tab with { IsStale = true } : tab).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static bool Holds(TabState tab, IReadOnlyList<string> keys)
    {
        return tab.Rows.Any(row => keys.Contains(row.Key, StringComparer.OrdinalIgnoreCase));
    }

    private static AppState MapRows(
        AppState state,
        IReadOnlyList<string> keys,
        Func<IssueSummary, IssueSummary> map)
    {
        var tabs = state.Session.Tabs;
        TabState[]? changed = null;

        for (var index = 0; index < tabs.Count; index++)
        {
            var rows = MapList(tabs[index].Rows, keys, map);

            if (ReferenceEquals(rows, tabs[index].Rows))
            {
                continue;
            }

            changed ??= [.. tabs];
            changed[index] = tabs[index] with { Rows = rows };
        }

        return changed is null ? state : state with { Session = state.Session with { Tabs = changed } };
    }

    private static IReadOnlyList<IssueSummary> MapList(
        IReadOnlyList<IssueSummary> rows,
        IReadOnlyList<string> keys,
        Func<IssueSummary, IssueSummary> map)
    {
        IssueSummary[]? copy = null;

        for (var index = 0; index < rows.Count; index++)
        {
            if (!keys.Contains(rows[index].Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            var mapped = map(rows[index]);

            if (ReferenceEquals(mapped, rows[index]))
            {
                continue;
            }

            copy ??= [.. rows];
            copy[index] = mapped;
        }

        return copy ?? rows;
    }

    private static Operation? Find(AppState state, long operationId)
    {
        return state.Session.Operations.FirstOrDefault(operation => operation.Id == operationId);
    }

    private static AppState ReplaceOperation(AppState state, Operation operation)
    {
        var operations = state.Session.Operations.Select(other => other.Id == operation.Id ? operation : other);

        return state with { Session = state.Session with { Operations = [.. operations] } };
    }

    private static ReduceResult Say(AppState state, string message)
    {
        return new ReduceResult(state with { Message = message }, _noEffects);
    }
}
