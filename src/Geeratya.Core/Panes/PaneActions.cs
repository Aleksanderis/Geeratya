using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Panes;

/// <summary>The pane actions on the active tab: <c>pane.focus-*</c>, <c>pane.zoom</c> and the zoom half of <c>Esc</c>.</summary>
public static class PaneActions
{
    /// <summary>True for the five pane action ids.</summary>
    public static bool IsPaneAction(string? action)
    {
        return action is ActionIds.PaneFocusLeft
            or ActionIds.PaneFocusDown
            or ActionIds.PaneFocusUp
            or ActionIds.PaneFocusRight
            or ActionIds.PaneZoom;
    }

    /// <summary>
    /// Runs a pane action on the active tab. Null when <paramref name="action"/> is not a pane action or the active
    /// tab shows no panes, so the caller can fall back to what the key meant before (a form's field step, nothing in
    /// a view).
    /// </summary>
    public static AppState? Apply(AppState state, string? action)
    {
        if (!IsPaneAction(action) || ActiveTab(state) is not { } tab || !tab.Panes.HasPanes)
        {
            return null;
        }

        var panes = action switch
        {
            ActionIds.PaneFocusLeft => PaneReducer.Focus(tab.Panes, PaneDirection.Left),
            ActionIds.PaneFocusDown => PaneReducer.Focus(tab.Panes, PaneDirection.Down),
            ActionIds.PaneFocusUp => PaneReducer.Focus(tab.Panes, PaneDirection.Up),
            ActionIds.PaneFocusRight => PaneReducer.Focus(tab.Panes, PaneDirection.Right),
            _ => PaneReducer.ToggleZoom(tab.Panes),
        };

        return ViewReducer.Replace(state, tab with { Panes = panes });
    }

    /// <summary>
    /// <c>Esc</c> while the active tab is zoomed: leaves the zoom (before it pops an issue or clears marks). Null
    /// when nothing is zoomed.
    /// </summary>
    public static AppState? LeaveZoom(AppState state)
    {
        if (ActiveTab(state) is not { } tab || tab.Panes.Zoomed is null)
        {
            return null;
        }

        return ViewReducer.Replace(state, tab with { Panes = PaneReducer.Unzoom(tab.Panes) });
    }

    /// <summary>True when the active tab has a zoomed pane.</summary>
    public static bool IsZoomed(AppState state)
    {
        return ActiveTab(state)?.Panes.Zoomed is not null;
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }
}
