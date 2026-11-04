namespace Geeratya.Core.Panes;

/// <summary>Pure transitions of a <see cref="PaneState"/>: focus, zoom and inner scroll.</summary>
public static class PaneReducer
{
    /// <summary>
    /// The state after a focus key. Unzoomed it moves over the grid; zoomed, left and right switch to the previous
    /// and next section of the strip (not wrapping) and up and down do nothing. Unchanged when there are no panes.
    /// </summary>
    public static PaneState Focus(PaneState state, PaneDirection direction)
    {
        if (state.Current is not { } current)
        {
            return state;
        }

        if (state.Zoomed is not null)
        {
            return Section(state, direction);
        }

        return state with { Focused = PaneNavigator.Move(state.Grid, current, direction) };
    }

    /// <summary>
    /// <c>pane.zoom</c>: zooms the focused pane, or leaves the zoom when one is active. Unchanged when there are no
    /// panes.
    /// </summary>
    public static PaneState ToggleZoom(PaneState state)
    {
        if (state.Zoomed is not null)
        {
            return Unzoom(state);
        }

        return state.Current is { } current ? state with { Focused = current, Zoomed = current } : state;
    }

    /// <summary>Leaves the zoom (<c>Esc</c>); the pane that was zoomed keeps the focus.</summary>
    public static PaneState Unzoom(PaneState state)
    {
        return state.Zoomed is { } zoomed ? state with { Focused = zoomed, Zoomed = null } : state;
    }

    /// <summary>Stores the scroll of a pane, clamped to its content.</summary>
    public static PaneState SetScroll(PaneState state, string id, PaneScroll scroll)
    {
        var scrolls = new Dictionary<string, PaneScroll>(state.Scrolls ?? new Dictionary<string, PaneScroll>())
        {
            [id] = scroll.Resized(scroll.Total, scroll.Viewport),
        };

        return state with { Scrolls = scrolls };
    }

    /// <summary>
    /// Replaces the grid after a layout change (a resize, a different screen): focus and zoom that no longer exist
    /// are dropped, and scrolls of vanished panes go with them.
    /// </summary>
    public static PaneState WithGrid(PaneState state, PaneGrid grid)
    {
        var next = state with { Grid = grid };
        var scrolls = state.Scrolls?.Where(entry => grid.Find(entry.Key) is not null).ToDictionary();

        return next with
        {
            Focused = next.Current,
            Zoomed = state.Zoomed is { } zoomed && grid.Find(zoomed) is not null ? zoomed : null,
            Scrolls = scrolls is { Count: > 0 } ? scrolls : null,
        };
    }

    private static PaneState Section(PaneState state, PaneDirection direction)
    {
        var step = direction switch
        {
            PaneDirection.Left => -1,
            PaneDirection.Right => 1,
            _ => 0,
        };

        var sections = state.Sections;
        var index = sections.ToList().IndexOf(state.Zoomed!);
        var target = index + step;

        if (step == 0 || index < 0 || target < 0 || target >= sections.Count)
        {
            return state;
        }

        return state with { Focused = sections[target], Zoomed = sections[target] };
    }
}
