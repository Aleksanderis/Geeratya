using Geeratya.Core.State;
using Geeratya.Core.Views;
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
        if (state.Session.Operations.Count == 0 && tab.ProtectedKeys.Count == 0)
        {
            return incoming;
        }

        var protectedKeys = ProtectedKeys(state, tab);

        if (tab.Rows.Count == 0)
        {
            return incoming;
        }

        var held = new Dictionary<string, IssueSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in tab.Rows)
        {
            if (protectedKeys.Contains(row.Key))
            {
                held[row.Key] = row;
            }
        }

        if (held.Count == 0)
        {
            return incoming;
        }

        return [.. incoming.Select(row => held.TryGetValue(row.Key, out var own) ? own : row)];
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
