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
        var tabs = state.Session.Tabs.Select(tab => Without(tab, keys)).ToArray();

        return state with { Session = state.Session with { Tabs = tabs } };
    }

    private static TabState Without(TabState tab, IReadOnlyList<string> keys)
    {
        var details = tab.Detail.Where(detail => !Contains(keys, detail.Key)).ToArray();
        var kept = tab.Rows.Where(row => !Contains(keys, row.Key)).ToArray();
        var removed = tab.Rows.Count - kept.Length;

        if (removed == 0)
        {
            return details.Length == tab.Detail.Count ? tab : tab with { Detail = details };
        }

        var total = tab.Total - removed;
        var shrunk = tab with { Rows = kept, Detail = details, Total = total is < 0 ? 0 : total, IsStale = true };

        return ViewReducer.KeepSelection(shrunk);
    }

    private static bool Contains(IReadOnlyList<string> keys, string key)
    {
        return keys.Contains(key, StringComparer.OrdinalIgnoreCase);
    }
}
