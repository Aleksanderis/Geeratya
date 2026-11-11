using Geeratya.Core.Selection;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Writes;

/// <summary>
/// Takes deleted issues out of every tab: the rows (selection follows its neighbour, the count shrinks), the open
/// details (an issue that no longer exists is not shown, so the stack pops back to what was under it), and marks
/// the tab stale so its next reload is trusted. Pure.
/// </summary>
public static class WriteRemoval
{
    public static AppState Apply(AppState state, IReadOnlyList<string> keys)
    {
        var set = new HashSet<string>(keys, StringComparer.OrdinalIgnoreCase);
        var tabs = state.Session.Tabs.Select(tab => Without(tab, keys, set)).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static TabState Without(TabState tab, IReadOnlyList<string> keys, HashSet<string> set)
    {
        // A load in flight may still bring the deleted issues back: its reply drops them (WriteMerge).
        var loading = tab.Load is Loading ? tab with { RemovedKeys = [.. tab.RemovedKeys.Union(keys)] } : tab;

        return RowSelection.Without(WithoutRows(loading, set), set);
    }

    private static TabState WithoutRows(TabState tab, HashSet<string> keys)
    {
        var details = tab.Detail.Where(detail => !keys.Contains(detail.Key)).ToArray();
        var kept = tab.Rows.Where(row => !keys.Contains(row.Key)).ToArray();
        var removed = tab.Rows.Count - kept.Length;

        if (removed == 0)
        {
            return details.Length == tab.Detail.Count ? tab : tab with { Detail = details };
        }

        var total = tab.Total - removed;

        if (total < 0)
        {
            total = 0;
        }

        var shrunk = tab with { Rows = kept, Detail = details, Total = total, IsStale = true };

        return ViewReducer.KeepSelection(shrunk);
    }
}
