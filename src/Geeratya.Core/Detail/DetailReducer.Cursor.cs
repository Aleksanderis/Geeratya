using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// Moves the cursor of the top issue of the active tab by line or page; the window follows it, scrolling only
    /// when the cursor would leave it.
    /// </summary>
    public static AppState MoveCursor(AppState state, TabState tab, ListMove move)
    {
        var top = tab.Detail[^1];
        var viewport = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));

        if (top.Document is not { } document)
        {
            return state;
        }

        if (top.SideCursor is { } sideCursor && document.Side is { } side)
        {
            var moved = top with { SideCursor = Target(side, sideCursor, move, viewport) };

            return ViewReducer.Replace(state, ReplaceTop(tab, moved));
        }

        if (MoveInDescription(state, tab, move) is { } inDescription)
        {
            return inDescription;
        }

        if (MoveInDescriptionPage(state, tab, move) is { } inDescriptionPage)
        {
            return inDescriptionPage;
        }

        if (MoveInComments(state, tab, move) is { } inComments)
        {
            return inComments;
        }

        if (MoveInZoomedPane(state, tab, move, viewport) is { } inZoomed)
        {
            return inZoomed;
        }

        var target = Target(document, top.Cursor, move, viewport);

        if (move is ListMove.PageDown or ListMove.PageUp && top.Zoomed is null)
        {
            target = InPane(document, top.Cursor, target, move);
        }

        return ViewReducer.Replace(state, ReplaceTop(tab, WithCursor(top, target, viewport)));
    }

    /// <summary>
    /// A half-page move never leaves the pane the cursor is in (#870): a target in another pane becomes the last
    /// (down) or first (up) stop of the cursor's own pane.
    /// </summary>
    private static int InPane(DetailDocument document, int cursor, int target, ListMove move)
    {
        var section = document.SectionAt(cursor);

        if (document.SectionAt(target) == section)
        {
            return target;
        }

        var own = document.Stops.Where(stop => document.SectionAt(stop) == section).ToArray();

        return own.Length == 0 ? cursor : move == ListMove.PageDown ? own[^1] : own[0];
    }

    private static int Target(DetailDocument document, int cursor, ListMove move, int viewport)
    {
        return move switch
        {
            ListMove.Down => document.StopAfter(cursor),
            ListMove.Up => document.StopBefore(cursor),
            ListMove.PageDown => PageDown(document, cursor, viewport),
            ListMove.PageUp => document.StopBefore(cursor - Math.Max(1, viewport / 2) + 1),
            ListMove.Top => document.Snap(0),
            _ => document.Stops.Count == 0 ? 0 : document.Stops[^1],
        };
    }

    /// <summary>
    /// A click: puts the cursor of the top issue on the document line shown at <paramref name="bodyRow"/> (the
    /// first row of the overlay is row 0), without scrolling; a click on a frame edge focuses its pane, a click on a
    /// section of the zoom strip switches to it. Null for cells outside the document.
    /// </summary>
    public static AppState? PlaceCursor(AppState state, TabState tab, int bodyRow, int bodyColumn = 0)
    {
        var top = tab.Detail[^1];
        var hit = DetailPointer.Locate(state, tab, bodyRow, bodyColumn);

        if (top.Document is not { } document || hit.Kind == DetailPointerKind.None)
        {
            return null;
        }

        if (hit.Kind == DetailPointerKind.Strip)
        {
            return hit.Section == top.Zoomed ? state : Reshow(state, tab, hit.Section, false);
        }

        if (hit.OnSide)
        {
            var side = document.Side!;
            var line = hit.Line >= 0 ? side.Snap(hit.Line) : top.SideCursor ?? side.Snap(0);

            return ViewReducer.Replace(state, ReplaceTop(tab, top with { SideCursor = line }));
        }

        var placed = top with { Cursor = StopFor(document, hit.Line), SideCursor = null };

        return ViewReducer.Replace(state, ReplaceTop(tab, placed));
    }

    // The stop a click on a line lands on: a row of a comment that is no stop (the cut body, its note) selects that
    // comment, so the stop before it, not the next comment's header that Snap would take.
    private static int StopFor(DetailDocument document, int line)
    {
        var onComment = line < document.Targets.Count && document.Targets[line] is { Kind: DetailTargetKind.Comment };

        return onComment && !document.Stops.Contains(line) ? document.StopBefore(line) : document.Snap(line);
    }

    /// <summary>
    /// A wheel notch over the detail: when the pointer is over a pane other than the one that holds the cursor, that
    /// pane takes the focus first (on the row under the pointer), so the notch scrolls the pane under the pointer.
    /// Over the pane that has the focus, a frame edge or anywhere outside the document nothing moves.
    /// </summary>
    public static AppState FocusUnderWheel(AppState state, TabState tab, int bodyRow, int bodyColumn)
    {
        var top = tab.Detail[^1];
        var hit = DetailPointer.Locate(state, tab, bodyRow, bodyColumn);

        if (top.Document is not { } document || hit.Kind is DetailPointerKind.None or DetailPointerKind.Strip)
        {
            return state;
        }

        var onSide = top.SideCursor is not null && document.Side is not null;

        if (hit.OnSide != onSide)
        {
            return PlaceCursor(state, tab, bodyRow, bodyColumn) ?? state;
        }

        var line = hit.Line >= 0 ? hit.Line : top.Cursor;
        var samePane = hit.OnSide || document.SectionAt(line) == document.SectionAt(top.Cursor);

        return samePane ? state : PlaceCursor(state, tab, bodyRow, bodyColumn) ?? state;
    }

    /// <summary>
    /// Puts the cursor on <paramref name="line"/> and scrolls so that it is the first visible line when the document
    /// is long enough (a pane jump shows the pane from its top edge).
    /// </summary>
    public static AppState JumpTo(AppState state, TabState tab, int line)
    {
        var top = tab.Detail[^1];
        var lines = top.Document?.Lines.Count ?? 0;
        var viewport = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));
        var cursor = top.Document?.Snap(Math.Clamp(line, 0, Math.Max(0, lines - 1))) ?? 0;
        var scroll = Math.Clamp(cursor, 0, Math.Max(0, lines - viewport));

        return ViewReducer.Replace(state, ReplaceTop(tab, top with { Cursor = cursor, Scroll = scroll }));
    }

    /// <summary>
    /// Puts the cursor on the first row of pane <paramref name="pane"/> (an index into
    /// <see cref="DetailDocument.SectionIds"/>) and scrolls so that the pane's top edge is the first visible line when
    /// the document is long enough.
    /// </summary>
    public static AppState JumpToPane(AppState state, TabState tab, int pane)
    {
        var top = tab.Detail[^1];

        if (top.Document is not { } document || pane < 0 || pane >= document.Sections.Count)
        {
            return state;
        }

        var viewport = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));
        var cursor = document.Snap(document.Sections[pane]);
        var scroll = Math.Clamp(document.PaneStarts[pane], 0, Math.Max(0, document.Lines.Count - viewport));

        return ViewReducer.Replace(state, ReplaceTop(tab, top with { Cursor = cursor, Scroll = scroll }));
    }

    /// <summary>Half a window down; a landing inside the field the cursor is on still moves past that field.</summary>
    private static int PageDown(DetailDocument document, int cursor, int viewport)
    {
        var target = document.Snap(cursor + Math.Max(1, viewport / 2));

        return target > cursor ? target : document.StopAfter((document.BlockAt(cursor)?.End ?? cursor + 1) - 1);
    }

    private static DetailState WithCursor(DetailState detail, int target, int viewport)
    {
        var lines = detail.Document?.Lines.Count ?? 0;
        var cursor = detail.Document?.Snap(Math.Clamp(target, 0, Math.Max(0, lines - 1))) ?? 0;
        var scroll = detail.Scroll;

        if (detail.Document is { Stops.Count: > 0 } stops && cursor == stops.Stops[^1])
        {
            scroll = lines;
        }
        else if (detail.Document is { Stops.Count: > 0 } first && cursor == first.Stops[0])
        {
            scroll = 0;
        }
        else if (cursor < scroll)
        {
            scroll = cursor;
        }
        else if (BottomOf(detail, cursor) >= scroll + viewport)
        {
            // The whole field shows, not only its first row (a field block is a label and its value rows).
            scroll = Math.Min(cursor, BottomOf(detail, cursor) - viewport + 1);
        }

        return detail with { Cursor = cursor, Scroll = Math.Clamp(scroll, 0, Math.Max(0, lines - viewport)) };
    }

    private static int BottomOf(DetailState detail, int cursor)
    {
        return (detail.Document?.BlockAt(cursor)?.End ?? cursor + 1) - 1;
    }

    /// <summary>
    /// After a viewport change: the top issue of every tab is laid out for the new width and re-clamped.
    /// </summary>
    public static TabState Resized(TabState tab, int columns, int listRows)
    {
        if (tab.Detail.Count == 0)
        {
            return tab;
        }

        var top = tab.Detail[^1];
        var width = columns > 0 ? columns : DetailLayout.DefaultWidth;
        var sameWidth = top.Document?.Width == width
            && top.Document.ViewportRows == DetailLayout.ViewportRows(listRows);
        var resized = sameWidth ? Clamp(top, listRows) : Layout(top, columns, listRows);

        return ReplaceTop(tab, resized);
    }

    /// <summary>What Enter opens on the cursor line of <paramref name="detail"/>, null on a plain line.</summary>
    public static DetailTarget? TargetOf(DetailState detail)
    {
        var onSide = detail.SideCursor is not null && detail.Document?.Side is not null;
        var targets = onSide ? detail.Document!.Side!.Targets : detail.Document?.Targets;
        var cursor = onSide ? detail.SideCursor!.Value : detail.Cursor;

        if (targets is null || cursor < 0 || cursor >= targets.Count)
        {
            return null;
        }

        return targets[cursor];
    }

    /// <summary>
    /// The board configuration arrived after issues were opened: the open details that fell back to the generic
    /// estimate label take the board's name for it, without reopening (#470).
    /// </summary>
    public static TabState WithEstimateLabel(TabState tab, int columns, int listRows)
    {
        var label = tab.Board?.EstimationFieldName;

        if (label is null || tab.Detail.All(detail => detail.EstimateLabel is not null))
        {
            return tab;
        }

        var stack = tab.Detail
            .Select(detail => detail.EstimateLabel is null
                ? Layout(detail with { EstimateLabel = label }, columns, listRows)
                : detail)
            .ToArray();

        return tab with { Detail = stack };
    }

    // The commentsRow is the Comments pane row the cursor goes to, when the caller knows it; else the cursor stays on
    // its row there.
    private static DetailState Layout(DetailState detail, int columns, int listRows, int? commentsRow = null)
    {
        var width = columns > 0 ? columns : DetailLayout.DefaultWidth;
        var viewport = DetailLayout.ViewportRows(listRows);
        var row = commentsRow ?? FollowedCommentsRow(detail, width);
        detail = ZoomWindowKept(detail, width, viewport);

        var document = IssueDocumentBuilder.Build(detail, width, viewport);

        // The zoomed pane is gone (its data went away): the zoom ends and the full layout shows.
        if (detail.Zoomed is not null && !document.IsZoomed)
        {
            return Layout(detail with { Zoomed = null }, columns, listRows + DetailZoom.StripRows, commentsRow);
        }

        var anchored = detail.Document is { } old && old.Width == width ? Anchored(detail, old, document) : detail.Cursor;
        var side = document.Side is null ? null : SideAnchored(detail, document.Side);
        var laidOut = Clamp(
            detail with { Document = document, Cursor = anchored, SideCursor = side, YankAnchor = null },
            listRows);

        return row is { } kept ? PlaceInComments(laidOut, kept, listRows, false) : laidOut;
    }

    /// <summary>
    /// The cursor line in <paramref name="document"/> that shows the same row as the cursor did in
    /// <paramref name="old"/> (same target, same offset inside a wrapped row), so data arriving above the cursor does
    /// not move it onto another row. A plain line keeps its line number.
    /// </summary>
    private static int Anchored(DetailState detail, DetailDocument old, DetailDocument document)
    {
        if (detail.Cursor < 0 || detail.Cursor >= old.Targets.Count || old.Targets[detail.Cursor] is not { } target)
        {
            return detail.Cursor;
        }

        var start = detail.Cursor;

        while (start > 0 && Equals(old.Targets[start - 1], target))
        {
            start--;
        }

        for (var line = 0; line < document.Targets.Count; line++)
        {
            if (Equals(document.Targets[line], target))
            {
                return Math.Min(line + detail.Cursor - start, document.Targets.Count - 1);
            }
        }

        return detail.Cursor;
    }

    private static DetailState Clamp(DetailState detail, int listRows)
    {
        var lines = detail.Document?.Lines.Count ?? 0;
        var viewport = DetailLayout.ViewportRows(listRows);
        var scrolled = detail with { Scroll = Math.Clamp(detail.Scroll, 0, Math.Max(0, lines - viewport)) };

        return WithCursor(scrolled, detail.Cursor, viewport);
    }

    private static TabState ReplaceTop(TabState tab, DetailState top)
    {
        var stack = tab.Detail.ToArray();

        stack[^1] = top;

        return tab with { Detail = stack };
    }

    private static TabState? ActiveTab(AppState state)
    {
        return state.Session.Active is { } id ? ViewReducer.Find(state.Session.Tabs, id) : null;
    }
}
