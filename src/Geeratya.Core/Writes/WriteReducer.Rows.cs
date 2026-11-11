using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Core.Writes;

public static partial class WriteReducer
{
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
        var set = KeySet(keys);

        return tab with
        {
            ProtectedKeys = [.. tab.ProtectedKeys.Union(keys, StringComparer.OrdinalIgnoreCase)],
            ProtectedRows = [.. tab.ProtectedRows.Where(row => !set.Contains(row.Key)), .. rows],
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

    /// <summary>The keys as one case-insensitive set, built once per call so a lookup per row is O(1).</summary>
    private static HashSet<string> KeySet(IEnumerable<string> keys)
    {
        return new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
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
        // One pass over the rows for all changes; the order part follows per change (it reads keys only).
        var patched = MapChanges(state, changes, false);

        foreach (var change in changes)
        {
            patched = Reorder(patched, change, false);
        }

        return patched;
    }

    /// <summary>
    /// The row part of the changes (patched, or reverted when <paramref name="undo"/>) in one pass over every tab:
    /// changes are indexed by key and applied in their given order per row, so the cost does not grow with
    /// rows times changes.
    /// </summary>
    private static AppState MapChanges(AppState state, IReadOnlyList<RowChange> changes, bool undo)
    {
        if (changes.Count == 0)
        {
            return state;
        }

        var byKey = new Dictionary<string, List<RowChange>>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in changes)
        {
            if (!byKey.TryGetValue(change.Key, out var list))
            {
                byKey[change.Key] = list = [];
            }

            list.Add(change);
        }

        return MapRows(state, KeySet(byKey.Keys), row => ApplyAll(row, byKey[row.Key], undo));
    }

    private static IssueSummary ApplyAll(IssueSummary row, List<RowChange> changes, bool undo)
    {
        var current = row;

        foreach (var change in changes)
        {
            current = undo ? change.Revert(current) : change.Patch(current);
        }

        return current;
    }

    private static AppState ReplaceRows(AppState state, IReadOnlyList<IssueSummary> fresh)
    {
        if (fresh.Count == 0)
        {
            return state;
        }

        var byKey = fresh.ToDictionary(row => row.Key, StringComparer.OrdinalIgnoreCase);

        return MapRows(state, KeySet(byKey.Keys), row => byKey.GetValueOrDefault(row.Key, row));
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

        var set = KeySet(operation.Keys);
        var tabs = state.Session.Tabs
            .Select(tab => IsRankOrdered(tab) && Holds(tab, set) ? tab with { RefreshQueued = true } : tab)
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

        var set = KeySet(operation.Keys);
        var tabs = state.Session.Tabs
            .Select(tab => Holds(tab, set) ? Unselected(tab, set) : tab)
            .ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static TabState Unselected(TabState tab, HashSet<string> keys)
    {
        var marks = tab.Marks.Where(mark => !keys.Contains(mark)).ToArray();

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
        var set = KeySet(keys);
        var tabs = state.Session.Tabs.Select(tab => Holds(tab, set) ? tab with { IsStale = true } : tab).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static bool Holds(TabState tab, HashSet<string> keys)
    {
        return tab.Rows.Any(row => keys.Contains(row.Key));
    }

    private static AppState MapRows(
        AppState state,
        HashSet<string> keys,
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
        HashSet<string> keys,
        Func<IssueSummary, IssueSummary> map)
    {
        IssueSummary[]? copy = null;

        for (var index = 0; index < rows.Count; index++)
        {
            // A parent and its sub-task can both be in the bulk: the row and its stubs are both mapped.
            var own = keys.Contains(rows[index].Key) ? map(rows[index]) : rows[index];
            var mapped = MapStubs(own, keys, map);

            if (ReferenceEquals(mapped, rows[index]))
            {
                continue;
            }

            copy ??= [.. rows];
            copy[index] = mapped;
        }

        return copy ?? rows;
    }

    /// <summary>
    /// The row with the status and title of its sub-task stubs mapped too: an expanded sub-task of a board card is not
    /// a row of the tab, it is listed from the parent's stub, so a write on it must change the stub to show (#645).
    /// </summary>
    private static IssueSummary MapStubs(IssueSummary parent, HashSet<string> keys, Func<IssueSummary, IssueSummary> map)
    {
        IssueSubtask[]? copy = null;

        for (var index = 0; index < parent.Subtasks.Count; index++)
        {
            var stub = parent.Subtasks[index];

            if (!keys.Contains(stub.Key))
            {
                continue;
            }

            var listed = BoardColumns.Listed(parent, stub);
            var mapped = map(listed);

            if (ReferenceEquals(mapped, listed))
            {
                continue;
            }

            copy ??= [.. parent.Subtasks];
            copy[index] = stub with { Status = mapped.Status, Summary = mapped.Summary };
        }

        return copy is null ? parent : parent with { Subtasks = copy };
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
