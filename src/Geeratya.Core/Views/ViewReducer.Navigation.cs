using Geeratya.Core.Backlog;
using Geeratya.Core.Boards;
using Geeratya.Core.Detail;
using Geeratya.Core.Lists;
using Geeratya.Core.State;

namespace Geeratya.Core.Views;

public static partial class ViewReducer
{
    private static AppState MoveSelection(AppState state, SelectionMoved moved, List<Effect> effects)
    {
        var tab = Find(state.Session.Tabs, moved.Tab);

        if (tab is null || tab.Rows.Count == 0)
        {
            return state;
        }

        var index = Math.Clamp(moved.Index, 0, tab.Rows.Count - 1);

        if (BacklogSections.Applies(tab))
        {
            var placed = BacklogCursor.Place(tab, index);
            var shown = Replace(state, placed);

            return WantsNextPage(shown, placed) ? RequestNext(shown, placed, effects) : shown;
        }

        var position = ListMotion.Reveal(new ListPosition(index, tab.Scroll), tab.Rows.Count, tab.VisibleRows);
        var selected = tab with { Selected = index, SelectedKey = tab.Rows[index].Key, Scroll = position.Scroll };
        var next = Replace(state, selected);

        return WantsNextPage(next, selected) ? RequestNext(next, selected, effects) : next;
    }

    /// <summary>
    /// Moves the selection of the active tab's list by a keyboard motion and goes through <see cref="SelectionMoved"/>,
    /// so scrolling and the next-page trigger have one path. Other layouts and tabs without rows are left alone.
    /// </summary>
    public static ReduceResult Navigate(AppState state, ListMove move)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is { Detail.Count: > 0 })
        {
            return new ReduceResult(DetailReducer.MoveCursor(state, tab, move), _noEffects);
        }

        if (tab is null || (tab.Rows.Count == 0 && !BacklogSections.Applies(tab)))
        {
            return new ReduceResult(state, _noEffects);
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return MovedInColumns(state, ColumnsNavigation.Move(tab, move));
        }

        if (BacklogSections.Applies(tab))
        {
            return MovedInColumns(state, BacklogCursor.Move(tab, move));
        }

        var position = ListMotion.Move(
            new ListPosition(tab.Selected, tab.Scroll),
            move,
            1,
            tab.Rows.Count,
            tab.VisibleRows);

        return Reduce(state, new SelectionMoved(tab.Id, position.Selected));
    }

    /// <summary>
    /// A click on the body of the active tab: selects the row or card under the pointer, or puts the cursor on the
    /// line of the opened issue. Null when the pointer is on nothing selectable (a blank row, a title, the area
    /// under the cards), so the caller knows no action may follow.
    /// </summary>
    public static ReduceResult? PlaceCursor(AppState state, PointerPlaced placed)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return null;
        }

        if (tab.Detail.Count > 0)
        {
            var box = DetailOverlayGeometry.Place(state.ViewportColumns, state.ViewportBodyHeight);
            var placedLine = DetailReducer.PlaceCursor(state, tab, placed.Row - box.Top, placed.Column - box.Left);

            return placedLine is null ? null : new ReduceResult(placedLine, _noEffects);
        }

        if (tab.Rows.Count == 0 && !BacklogSections.Applies(tab))
        {
            return null;
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            if (ColumnsNavigation.LaneAt(tab, placed.Width, placed.Column, placed.Row) is { } laneKey)
            {
                var shown = ColumnsNavigation.SwitchLane(tab, laneKey);

                return new ReduceResult(Replace(state, shown), _noEffects);
            }

            var moved = ColumnsNavigation.Place(tab, placed.Width, placed.Column, placed.Row);

            return ReferenceEquals(moved, tab) ? null : MovedInColumns(state, moved);
        }

        if (BacklogSections.Applies(tab))
        {
            var line = tab.Scroll + placed.Row - ListLayout.HeaderRows;

            if (placed.Row < ListLayout.HeaderRows || line >= tab.Scroll + tab.VisibleRows)
            {
                return null;
            }

            var layout = BacklogSections.Build(tab);

            if (BacklogCursor.EmptySprintAtLine(layout, line) is { } sprintId)
            {
                return new ReduceResult(Replace(state, BacklogCursor.FocusSprint(tab, sprintId)), _noEffects);
            }

            return BacklogCursor.RowAtLine(layout, line) is { } found
                ? Reduce(state, new SelectionMoved(tab.Id, found))
                : null;
        }

        var index = tab.Scroll + placed.Row - ListLayout.HeaderRows;

        if (placed.Row < ListLayout.HeaderRows || index >= tab.Rows.Count || index >= tab.Scroll + tab.VisibleRows)
        {
            return null;
        }

        return Reduce(state, new SelectionMoved(tab.Id, index));
    }

    /// <summary>Enter: opens the selected row as an issue, or loads more comments inside an issue.</summary>
    public static ReduceResult OpenSelected(AppState state)
    {
        var effects = new List<Effect>();
        var next = DetailReducer.OpenSelected(state, effects);

        return new ReduceResult(next, effects.Count == 0 ? _noEffects : effects);
    }

    /// <summary>Whether Esc has an issue to pop on the active tab.</summary>
    public static bool CanGoBack(AppState state)
    {
        return DetailReducer.CanGoBack(state);
    }

    /// <summary>
    /// Moves the selection of the active tab between the columns of its board (<c>h</c>, <c>l</c>, <c>0</c>,
    /// <c>$</c>). The list layout has no columns and ignores it.
    /// </summary>
    public static ReduceResult NavigateColumn(AppState state, BoardColumnMove move)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null || tab.Definition.Layout != ViewLayout.Columns || tab.Rows.Count == 0)
        {
            return new ReduceResult(state, _noEffects);
        }

        return MovedInColumns(state, ColumnsNavigation.MoveColumn(tab, move));
    }

    /// <summary>
    /// Switches the active tab between its layouts (<c>view.layout-toggle</c>). Only a board has a columns layout;
    /// any other source says so.
    /// </summary>
    public static ReduceResult ToggleLayout(AppState state)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        var layout = tab.Definition.Layout == ViewLayout.Columns ? ViewLayout.List : ViewLayout.Columns;

        return SetLayout(state, layout);
    }

    /// <summary>Switches the active tab to <paramref name="layout"/> (<c>:layout list|columns</c>).</summary>
    public static ReduceResult SetLayout(AppState state, ViewLayout layout)
    {
        var tab = state.Session.Active is { } id ? Find(state.Session.Tabs, id) : null;

        if (tab is null)
        {
            return new ReduceResult(state, _noEffects);
        }

        if (layout == ViewLayout.Columns && tab.Definition.Source is not (ViewSourceKind.Board or ViewSourceKind.Sprint))
        {
            return new ReduceResult(state with { Message = NoColumnsText }, _noEffects);
        }

        return Reduce(state, new ViewLayoutChanged(tab.Id, layout));
    }

    /// <summary>Puts the cursor of a columns tab on the card with that key (a flash jump), as a keyboard move would.</summary>
    public static ReduceResult SelectCard(AppState state, TabId tab, string key, string? laneKey = null)
    {
        var found = Find(state.Session.Tabs, tab);

        return found is null ? new ReduceResult(state, _noEffects) : MovedInColumns(state, ColumnsNavigation.Select(found, key, laneKey));
    }

    private static ReduceResult MovedInColumns(AppState state, TabState moved)
    {
        var effects = new List<Effect>();
        var next = Replace(state, moved);

        if (WantsNextPage(next, moved))
        {
            next = RequestNext(next, moved, effects);
        }

        return new ReduceResult(next, effects.Count == 0 ? _noEffects : effects);
    }

    /// <summary>
    /// Makes the tab <paramref name="delta"/> places from the active one active, wrapping around the ends. Nothing
    /// happens with fewer than two tabs.
    /// </summary>
    public static ReduceResult StepTab(AppState state, int delta)
    {
        var tabs = state.Session.Tabs;
        var current = state.Session.Active is { } id ? IndexOf(tabs, id) : -1;

        if (tabs.Count < 2 || current < 0)
        {
            return new ReduceResult(state, _noEffects);
        }

        var target = (((current + delta) % tabs.Count) + tabs.Count) % tabs.Count;

        return Reduce(state, new TabActivated(tabs[target].Id));
    }

    /// <summary>
    /// Flips to the tab that was active before the current one (the other tab with the newest
    /// <see cref="TabState.LastActiveSeq"/>); a closed tab is gone, so it is never picked. With no other tab the
    /// status line says so.
    /// </summary>
    public static ReduceResult LastActiveTab(AppState state)
    {
        var previous = state.Session.Tabs
            .Where(tab => tab.Id != state.Session.Active)
            .MaxBy(tab => tab.LastActiveSeq);

        if (previous is null || state.Session.Active is null)
        {
            return new ReduceResult(state with { Message = "No previous tab." }, _noEffects);
        }

        return Reduce(state, new TabActivated(previous.Id));
    }

    /// <summary>Makes the tab with the 1-based <paramref name="number"/> active; a missing number says so.</summary>
    public static ReduceResult GoToTab(AppState state, int number)
    {
        var tabs = state.Session.Tabs;

        if (number < 1 || number > tabs.Count)
        {
            return new ReduceResult(state with { Message = $"No tab {number}." }, _noEffects);
        }

        return Reduce(state, new TabActivated(tabs[number - 1].Id));
    }

    /// <summary>Closes the active tab (cancelling its work); without one the status line says so.</summary>
    public static ReduceResult CloseActiveTab(AppState state)
    {
        if (state.Session.Active is not { } id)
        {
            return new ReduceResult(state with { Message = "No tab to close." }, _noEffects);
        }

        return Reduce(state, new TabClosed(id));
    }
}
