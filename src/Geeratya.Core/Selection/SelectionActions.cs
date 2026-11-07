using Geeratya.Core.Backlog;
using Geeratya.Core.Keymap;
using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Selection;

/// <summary>
/// Visual mode and marks (<c>docs/internal/ux-keymap.md</c>, "Selection and counts"): the keys that build the set, the Esc
/// that clears it and the gate that makes every single-issue write refuse while a set is active. Visual mode is a
/// state of the tab (<see cref="TabState.VisualAnchor"/>) over Normal mode, so every Normal motion extends the range.
/// Pure.
/// </summary>
public static class SelectionActions
{
    private const string InsideIssueText = "Select rows in the list, not inside an issue";
    private const string ColumnsText = "Visual mode works in the list and backlog layouts; mark cards with Alt-m";
    private const string ListedText = "A sub-task listed under a card cannot be marked; Enter opens it";
    private const string NothingText = "Nothing to select here";

    private static readonly IReadOnlyList<Effect> _noEffects = [];

    /// <summary>
    /// The actions that write to one issue. With a set active they refuse instead of acting on the cursor row or on
    /// every selected issue.
    /// </summary>
    private static readonly HashSet<string> _singleIssueWrites =
    [
        ActionIds.RankTop,
        ActionIds.RankBottom,
        ActionIds.RankTo,
        ActionIds.IssueTransition,
        ActionIds.IssueTransitionNext,
        ActionIds.IssueTransitionPrevious,
        ActionIds.IssueDelete,
        ActionIds.IssueCreateSubtask,
        ActionIds.IssueLink,
        ActionIds.IssueUnlink,
        ActionIds.IssueEdit,
        ActionIds.IssueAssign,
        ActionIds.IssueAssignMe,
        ActionIds.CommentAdd,
        ActionIds.CommentEdit,
        ActionIds.CommentDelete,
    ];

    /// <summary>
    /// The refusal for a single-issue write while a set is active at the view root, else null. A key never widens or
    /// narrows its target silently.
    /// </summary>
    public static string? Refusal(AppState state, string action)
    {
        if (state.Mode != InputMode.Normal || !_singleIssueWrites.Contains(action))
        {
            return null;
        }

        var tab = ActiveTab(state);

        if (tab is null || tab.Detail.Count > 0 || !RowSelection.HoldsASet(tab))
        {
            return null;
        }

        return RowSelection.RefusalText(tab);
    }

    /// <summary><c>v</c>: starts Visual mode on the cursor row, or leaves it.</summary>
    public static ReduceResult ToggleVisual(AppState state)
    {
        var tab = Target(state, out var refusal, allowColumns: false);

        if (tab is null)
        {
            return Say(state, refusal!);
        }

        if (tab.VisualAnchor is not null)
        {
            return Result(ViewReducer.Replace(state, tab with { VisualAnchor = null }));
        }

        return Result(ViewReducer.Replace(state, tab with { VisualAnchor = CursorKey(tab) }));
    }

    /// <summary>
    /// <c>Alt-m</c>: marks the cursor row, or unmarks it, and moves down when <paramref name="advance"/> is set (the
    /// mouse marks in place). In Visual mode it marks the whole range (unmarks it when every row of it is marked)
    /// and leaves Visual mode.
    /// </summary>
    public static ReduceResult ToggleMark(AppState state, bool advance)
    {
        var tab = Target(state, out var refusal, allowColumns: true);

        if (tab is null)
        {
            return Say(state, refusal!);
        }

        if (CursorKey(tab) is not { } cursorKey)
        {
            return Say(state, NothingText);
        }

        if (tab.Definition.Layout == ViewLayout.Columns
            && ColumnsNavigation.SelectedIssue(tab) is { IsListed: true })
        {
            return Say(state, ListedText);
        }

        if (tab.VisualAnchor is not null && advance)
        {
            var keys = RowSelection.Targets(tab with { Marks = [] });

            return Result(ViewReducer.Replace(state, Toggled(tab, keys) with { VisualAnchor = null }));
        }

        var marked = ViewReducer.Replace(state, Toggled(tab, [cursorKey]));

        return advance ? ViewReducer.Navigate(marked, ListMove.Down) : Result(marked);
    }

    /// <summary>
    /// <c>Esc</c> at the view root: leaves Visual mode, else clears the marks. Null when nothing is selected, so the
    /// caller goes on with what <c>Esc</c> does otherwise.
    /// </summary>
    public static AppState? Back(AppState state)
    {
        var tab = ActiveTab(state);

        if (state.Mode != InputMode.Normal || tab is null || tab.Detail.Count > 0 || tab.RankPickup is not null)
        {
            return null;
        }

        if (tab.VisualAnchor is not null)
        {
            return ViewReducer.Replace(state, tab with { VisualAnchor = null });
        }

        return tab.Marks.Count > 0 ? ViewReducer.Replace(state, tab with { Marks = [] }) : null;
    }

    /// <summary>True when <c>Esc</c> has a selection to clear in the active tab at the view root.</summary>
    public static bool CanBack(AppState state)
    {
        var tab = ActiveTab(state);

        return tab is { Detail.Count: 0 } && (tab.VisualAnchor is not null || tab.Marks.Count > 0);
    }

    /// <summary>
    /// Rows dragged over with the pointer: Visual mode from the row where the button went down to the row where it
    /// was released. Nothing happens outside the list and backlog layouts.
    /// </summary>
    public static ReduceResult Dragged(AppState state, RowsDragged drag)
    {
        var started = ViewReducer.PlaceCursor(state, new PointerPlaced(drag.Column, drag.FromRow, drag.Width));

        if (started is null || state.Mode != InputMode.Normal || Target(started.State, out _, allowColumns: false) is not { } tab)
        {
            return Result(state);
        }

        var anchored = ViewReducer.Replace(started.State, tab with { VisualAnchor = CursorKey(tab) });
        var ended = ViewReducer.PlaceCursor(anchored, new PointerPlaced(drag.Column, drag.ToRow, drag.Width));

        if (ended is null)
        {
            return new ReduceResult(anchored, started.Effects);
        }

        return new ReduceResult(ended.State, [.. started.Effects, .. ended.Effects]);
    }

    /// <summary>
    /// The held button moved: the first move starts Visual mode on the pressed row, later ones keep the anchor and move
    /// the cursor to the row under the pointer. On a Scrum backlog the cursor never leaves the pressed row's section.
    /// </summary>
    public static ReduceResult Dragging(AppState state, RowsDragging drag)
    {
        if (drag.First)
        {
            var started = Dragged(state, new RowsDragged(drag.Column, drag.FromRow, drag.FromRow, drag.Width));

            return drag.ToRow == drag.FromRow ? started : Dragging(started.State, drag with { First = false });
        }

        if (ActiveTab(state) is not { VisualAnchor: not null } tab || state.Mode != InputMode.Normal
            || tab.Detail.Count > 0)
        {
            return Result(state);
        }

        if (BacklogSections.Applies(tab))
        {
            return SectionBound(state, tab, drag);
        }

        return ViewReducer.PlaceCursor(state, new PointerPlaced(drag.Column, drag.ToRow, drag.Width)) ?? Result(state);
    }

    /// <summary>
    /// The button came up after a live range: the cursor goes to the release row when the pointer is on a row, and the
    /// range stays. Without a range (nothing started it) the drag selects from press to release as before.
    /// </summary>
    public static ReduceResult DragEnded(AppState state, RowsDragged drag)
    {
        if (ActiveTab(state) is not { VisualAnchor: not null } || state.Mode != InputMode.Normal)
        {
            return Dragged(state, drag);
        }

        return Dragging(state, new RowsDragging(drag.Column, drag.FromRow, drag.ToRow, drag.Width, false));
    }

    /// <summary>
    /// One auto-scroll step: the cursor moves by the rows (clamped to the list) and the range follows. On a Scrum
    /// backlog the window scrolls by display lines (the cursor is a row there) and the cursor takes the row at the
    /// window edge, clamped to the section of the pressed row, so a long drag can reach another section's header.
    /// </summary>
    public static ReduceResult DragScrolled(AppState state, RowsDragScrolled scrolled)
    {
        if (ActiveTab(state) is not { VisualAnchor: not null } tab || state.Mode != InputMode.Normal
            || tab.Detail.Count > 0 || tab.Rows.Count == 0 || tab.Definition.Layout == ViewLayout.Columns)
        {
            return Result(state);
        }

        if (BacklogSections.Applies(tab))
        {
            return SectionScrolled(state, tab, scrolled.Rows);
        }

        var target = Math.Clamp(tab.Selected + scrolled.Rows, 0, tab.Rows.Count - 1);

        return target == tab.Selected ? Result(state) : ViewReducer.Reduce(state, new SelectionMoved(tab.Id, target));
    }

    // The section of the pressed row (the Visual anchor), null when it is not found.
    private static BacklogSection? AnchorSection(TabState tab, BacklogLayout layout)
    {
        return BacklogCursor.IndexOfKey(tab, tab.VisualAnchor) is { } anchor ? layout.SectionOfRow(anchor) : null;
    }

    // A live range on a sectioned backlog never spans sections: a pointer in another one holds the cursor at the edge
    // of the pressed row's section (the release there still asks to move the range, SprintMoveActions.Dropped).
    private static ReduceResult SectionBound(AppState state, TabState tab, RowsDragging drag)
    {
        var layout = BacklogSections.Build(tab);
        var line = tab.Scroll + drag.ToRow - ListLayout.HeaderRows;

        if (AnchorSection(tab, layout) is not { HasRows: true } home)
        {
            return Result(state);
        }

        if (layout.LineAt(line) is not { } to)
        {
            return Result(state);
        }

        if (to.Section.Key == home.Key)
        {
            return ViewReducer.PlaceCursor(state, new PointerPlaced(drag.Column, drag.ToRow, drag.Width)) ?? Result(state);
        }

        if (drag.ToRow < ListLayout.HeaderRows || line >= tab.Scroll + tab.VisibleRows)
        {
            return Result(state);
        }

        var edge = layout.StopFor(line < home.FirstLine ? home.FirstRow : home.FirstRow + home.RowCount - 1);

        return edge == tab.Selected ? Result(state) : ViewReducer.Reduce(state, new SelectionMoved(tab.Id, edge));
    }

    private static ReduceResult SectionScrolled(AppState state, TabState tab, int step)
    {
        var layout = BacklogSections.Build(tab);

        if (AnchorSection(tab, layout) is not { HasRows: true } home)
        {
            return Result(state);
        }

        var last = home.FirstRow + home.RowCount - 1;
        var scroll = Math.Clamp(tab.Scroll + step, 0, Math.Max(0, layout.LineCount - tab.VisibleRows));
        var edgeLine = step < 0 ? scroll : Math.Min(layout.LineCount - 1, scroll + tab.VisibleRows - 1);
        var row = edgeLine < home.FirstLine
            ? home.FirstRow
            : edgeLine >= home.FirstLine + home.LineCount
                ? last
                : Math.Clamp(BacklogCursor.RowAtLine(layout, edgeLine) ?? home.FirstRow, home.FirstRow, last);
        var edge = layout.StopFor(row);

        if (scroll == tab.Scroll && edge == tab.Selected)
        {
            return Result(state);
        }

        // The cursor move goes through the same path as a click (next-page trigger included); the window is set after
        // it, because the cursor may be off screen by design while the pointer is far from its section.
        var moved = edge == tab.Selected ? Result(state) : ViewReducer.Reduce(state, new SelectionMoved(tab.Id, edge));

        return ActiveTab(moved.State) is { } after
            ? new ReduceResult(ViewReducer.Replace(moved.State, after with { Scroll = scroll }), moved.Effects)
            : moved;
    }

    private static TabState Toggled(TabState tab, IReadOnlyList<string> keys)
    {
        var allMarked = keys.All(key => tab.Marks.Contains(key, StringComparer.OrdinalIgnoreCase));

        if (allMarked)
        {
            return tab with
            {
                Marks = [.. tab.Marks.Where(mark => !keys.Contains(mark, StringComparer.OrdinalIgnoreCase))],
            };
        }

        var added = keys.Where(key => !tab.Marks.Contains(key, StringComparer.OrdinalIgnoreCase));

        return tab with { Marks = [.. tab.Marks, .. added] };
    }

    private static TabState? Target(AppState state, out string? refusal, bool allowColumns)
    {
        refusal = null;

        var tab = ActiveTab(state);

        if (tab is null || tab.Rows.Count == 0)
        {
            refusal = NothingText;

            return null;
        }

        if (tab.Detail.Count > 0)
        {
            refusal = InsideIssueText;

            return null;
        }

        if (!allowColumns && tab.Definition.Layout == ViewLayout.Columns)
        {
            refusal = ColumnsText;

            return null;
        }

        return tab;
    }

    private static string? CursorKey(TabState tab)
    {
        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ColumnsNavigation.SelectedIssue(tab)?.Key;
        }

        return tab.Rows[Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1)].Key;
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }

    private static ReduceResult Say(AppState state, string text)
    {
        return new ReduceResult(state with { Message = text }, _noEffects);
    }

    private static ReduceResult Result(AppState state)
    {
        return new ReduceResult(state, _noEffects);
    }
}
