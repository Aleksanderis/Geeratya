using System.Globalization;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Links;
using Geeratya.Core.Sprints;
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
    private const string ConfirmWriteAction = "write";
    private const string NoProfileText = "No profile is active.";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    public static ReduceResult Reduce(AppState state, WriteCommand command)
    {
        return CommentEditing.Settled(state, ReduceCore(state, command), command);
    }

    private static ReduceResult ReduceCore(AppState state, WriteCommand command)
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

        if (!requested.Confirmed && write.AskText is { } question)
        {
            return Ask(state, requested, question);
        }

        var id = state.Sequence + 1;
        var changes = Plan(session.Tabs, write);
        var operation = new Operation(id, write, requested.Tab, OperationPhase.Sending, changes, requested.At);
        var patched = FollowCard(Patch(state, changes), write, requested.Tab);

        var next = patched with
        {
            Sequence = id,
            Message = write.StartedText,
            Session = patched.Session with { Operations = [.. session.Operations, operation] },
        };

        return new ReduceResult(next, [new ExecuteWriteEffect(id, write)]);
    }

    /// <summary>Opens Confirm mode; <c>y</c> runs the same request again, marked as confirmed.</summary>
    private static ReduceResult Ask(AppState state, WriteRequested requested, string question)
    {
        var label = requested.Request.AskLabel;
        var again = requested with { Confirmed = true };
        var confirm = new ConfirmRequest(question, ConfirmWriteAction, null, again, label);
        var asking = state with
        {
            Mode = InputMode.Confirm,
            Confirm = confirm,
            PendingKeys = string.Empty,
            Message = null,
        };

        return new ReduceResult(asking, _noEffects);
    }

    /// <summary>
    /// A card that changes status leaves its column: the cursor of the tab that started the move follows the card
    /// into its new column (and back again if the move is rolled back) and keeps the scroll of every column. Only
    /// while the card is still the tab's selection.
    /// </summary>
    private static AppState FollowCard(AppState state, WriteRequest request, TabId tabId)
    {
        if (request.Kind != OperationKind.Transition ||
            request.Keys.Count == 0 ||
            ViewReducer.Find(state.Session.Tabs, tabId) is not { Cursor: not null } tab ||
            !string.Equals(tab.SelectedKey, request.Keys[0], StringComparison.OrdinalIgnoreCase))
        {
            return state;
        }

        return ViewReducer.Replace(state, ColumnsNavigation.FollowCard(tab, request.Keys[0]));
    }

    private static string? BusyText(IReadOnlyList<Operation> operations, WriteRequest write)
    {
        foreach (var operation in operations)
        {
            if (write is SprintWriteRequest sprint
                && operation.Request is SprintWriteRequest running
                && running.BoardId == sprint.BoardId)
            {
                return $"The board is busy ({running.BusyText})";
            }

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

        var moved = operation.Keys.Where(key => !IsAmong(failed, key)).ToArray();

        if (moved.Length == 0)
        {
            return state;
        }

        var tabs = state.Session.Tabs
            .Select(tab => Holds(tab, moved) ? Unselected(tab, moved) : tab)
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
        var reverted = state;

        foreach (var change in operation.Undo)
        {
            reverted = MapRows(reverted, [change.Key], change.Revert);
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
            patched = Reorder(patched, change, false);
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

    /// <summary>
    /// A rank that Jira refused because the issues changed (a reference that is gone, a changed issue) leaves the
    /// shown order doubtful, so the tabs reload; permission, rate limit and offline refusals do not.
    /// </summary>
    private static bool ReloadsAfter(WriteError error)
    {
        return error.Kind is WriteErrorKind.Validation or WriteErrorKind.NotFound or WriteErrorKind.Conflict;
    }

    /// <summary>
    /// Rank changes the order, which a re-read of one issue cannot show: after the operation the tabs whose order is
    /// the rank reload (queued, so it runs when the last write ends), and the server order is the truth.
    /// </summary>
    private static AppState QueueRankReload(AppState state, Operation operation)
    {
        if (operation.Kind is not (OperationKind.Rank or OperationKind.SprintMove or OperationKind.RankMove))
        {
            return state;
        }

        var tabs = state.Session.Tabs
            .Select(tab => IsRankOrdered(tab) && Holds(tab, operation.Keys) ? tab with { RefreshQueued = true } : tab)
            .ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    /// <summary>
    /// After a sprint move the moved issues leave the marks and the Visual range of every tab (they are in their new
    /// place); other writes keep the selection.
    /// </summary>
    private static AppState ClearSelection(AppState state, Operation operation)
    {
        if (operation.Kind is not (OperationKind.SprintMove or OperationKind.RankMove))
        {
            return state;
        }

        var tabs = state.Session.Tabs
            .Select(tab => Holds(tab, operation.Keys) ? Unselected(tab, operation.Keys) : tab)
            .ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static TabState Unselected(TabState tab, IReadOnlyList<string> keys)
    {
        var marks = tab.Marks.Where(mark => !IsAmong(keys, mark)).ToArray();

        return tab with { Marks = marks, VisualAnchor = null };
    }

    private static bool IsRankOrdered(TabState tab)
    {
        return tab.Definition.Source.IsBoardBased();
    }

    /// <summary>Applies or undoes the order part of a change in every tab; the selection follows its issue.</summary>
    private static AppState Reorder(AppState state, RowChange change, bool undo)
    {
        var tabs = state.Session.Tabs;
        TabState[]? changed = null;

        for (var index = 0; index < tabs.Count; index++)
        {
            var rows = undo ? change.RevertOrder(tabs[index]) : change.PatchOrder(tabs[index]);

            if (ReferenceEquals(rows, tabs[index].Rows))
            {
                continue;
            }

            changed ??= [.. tabs];
            changed[index] = ViewReducer.KeepSelection(tabs[index] with { Rows = rows });
        }

        return changed is null ? state : state with { Session = state.Session with { Tabs = changed } };
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
