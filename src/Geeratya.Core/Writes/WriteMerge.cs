using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// The merge rule for a load that lands while a write runs (or that started before one ended): for an issue under an
/// operation the tab keeps its current, possibly optimistic row and takes every other row from the reply, so the
/// card does not flicker back. The finish of the write brings the truth for that issue.
/// </summary>
public static class WriteMerge
{
    /// <summary>
    /// <paramref name="incoming"/> with the tab's own row for every key that is under an operation or was under one
    /// when this tab's load was in flight. Returns <paramref name="incoming"/> itself when no key is protected.
    /// </summary>
    public static IReadOnlyList<IssueSummary> KeepHeldRows(
        AppState state,
        TabState tab,
        IReadOnlyList<IssueSummary> incoming)
    {
        if (tab.RemovedKeys.Count > 0)
        {
            var removed = new HashSet<string>(tab.RemovedKeys, StringComparer.OrdinalIgnoreCase);

            incoming = [.. incoming.Where(row => !removed.Contains(row.Key))];
        }

        if (state.Session.Operations.Count == 0 && tab.ProtectedKeys.Count == 0 && tab.ProtectedRows.Count == 0)
        {
            return incoming;
        }

        var protectedKeys = ProtectedKeys(state, tab);
        var held = new Dictionary<string, IssueSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in tab.ProtectedRows)
        {
            held[row.Key] = row;
        }

        foreach (var row in tab.Rows)
        {
            if (protectedKeys.Contains(row.Key))
            {
                held[row.Key] = row;
            }
        }

        var running = RunningByKey(state);

        if (held.Count == 0 && running.Count == 0)
        {
            return incoming;
        }

        return [.. incoming.Select(row => Held(row, held, running))];
    }

    /// <summary>
    /// <paramref name="incoming"/> with the fields that writes changed kept: the running operations' optimistic
    /// values and the changes of writes that ended while this request was in flight. Returns the same instance when
    /// nothing applies.
    /// </summary>
    public static IssueDetail KeepHeldFields(AppState state, DetailState detail, IssueDetail incoming)
    {
        var ended = KeepEndedFields(detail, incoming);
        var running = RunningChanges(state).Where(change => IsFor(change, incoming.Key));

        return running.Aggregate(ended, (current, change) => change.Patch(current));
    }

    /// <summary>
    /// Only the changes of writes that ended and succeeded: what may be cached, because a running write may still
    /// fail and its optimistic value must never reach the disk.
    /// </summary>
    public static IssueDetail KeepEndedFields(DetailState detail, IssueDetail incoming)
    {
        var ended = (detail.HeldChanges ?? []).Where(change => IsFor(change, incoming.Key));

        return ended.Aggregate(incoming, (current, change) => change.Patch(current));
    }

    private static IssueSummary Held(
        IssueSummary row,
        Dictionary<string, IssueSummary> held,
        Dictionary<string, List<RowChange>> running)
    {
        if (held.TryGetValue(row.Key, out var own))
        {
            return own;
        }

        // A tab that does not hold the issue yet (a next page) takes the optimistic value of a running write.
        if (!running.TryGetValue(row.Key, out var changes))
        {
            return row;
        }

        return changes.Aggregate(row, (current, change) => change.Patch(current));
    }

    /// <summary>The running operations' changes indexed by key (in operation order), so a row looks its changes up.</summary>
    private static Dictionary<string, List<RowChange>> RunningByKey(AppState state)
    {
        var byKey = new Dictionary<string, List<RowChange>>(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in state.Session.Operations)
        {
            foreach (var change in operation.Undo)
            {
                if (!byKey.TryGetValue(change.Key, out var list))
                {
                    byKey[change.Key] = list = [];
                }

                list.Add(change);
            }
        }

        return byKey;
    }

    private static List<RowChange> RunningChanges(AppState state)
    {
        return [.. state.Session.Operations.SelectMany(operation => operation.Undo)];
    }

    private static bool IsFor(RowChange change, string key)
    {
        return string.Equals(change.Key, key, StringComparison.OrdinalIgnoreCase);
    }

    private static HashSet<string> ProtectedKeys(AppState state, TabState tab)
    {
        var keys = new HashSet<string>(tab.ProtectedKeys, StringComparer.OrdinalIgnoreCase);

        foreach (var operation in state.Session.Operations)
        {
            keys.UnionWith(operation.Keys);
        }

        return keys;
    }
}
