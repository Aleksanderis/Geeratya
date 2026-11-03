using Geeratya.Core.State;

namespace Geeratya.Core.Views;

/// <summary>
/// <c>tab.rename</c>: the name a user gives the active tab (#574). Local only: nothing is sent to Jira, and the name is
/// kept with the remembered tabs.
/// </summary>
public static class TabRenameReducer
{
    private const string NoTabMessage = "No tab to rename: open one first.";

    /// <summary>The command line text that starts a rename: the current name, empty while the tab shows its source label.</summary>
    public static string CommandFor(TabState tab)
    {
        return $"tab rename {tab.Name}".TrimEnd();
    }

    /// <summary>The active tab, or null when none is open.</summary>
    public static TabState? ActiveTab(AppState state)
    {
        return state.Session.Tabs.FirstOrDefault(tab => tab.Id == state.Session.Active);
    }

    /// <summary>The reply when no tab is open.</summary>
    public static ReduceResult NoTab(AppState state)
    {
        return new ReduceResult(state with { Message = NoTabMessage, MessageSeverity = StatusSeverity.Warning }, []);
    }

    /// <summary>Names the active tab; an empty name restores the source label.</summary>
    public static ReduceResult Rename(AppState state, string name)
    {
        if (ActiveTab(state) is not { } tab)
        {
            return NoTab(state);
        }

        var clean = TabNames.Clean(name);
        var message = clean is null ? "Tab name cleared: showing the source label." : $"Tab renamed to \"{clean}\".";
        var renamed = ViewReducer.Replace(state, tab with { Name = clean });

        return new ReduceResult(renamed with { Message = message }, []);
    }
}
