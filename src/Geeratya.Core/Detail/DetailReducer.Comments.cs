using Geeratya.Core.Lists;
using Geeratya.Core.Panes;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// The cursor keys inside the Comments pane: <c>j</c>/<c>k</c> go between the stops of the pane's content (the
    /// header of a cut comment, every line of an expanded one) and the pane scrolls by itself to keep the cursor in
    /// view. Null when the cursor is not in the pane or the key leaves it (<c>k</c> on its first stop), so the
    /// document moves as usual.
    /// </summary>
    private static AppState? MoveInComments(AppState state, TabState tab, ListMove move)
    {
        var top = tab.Detail[^1];
        var row = CommentsRowOf(top);

        if ((row is null && move != ListMove.Bottom) || move == ListMove.Top || top.Document is not { } document
            || !document.SectionIds.Contains(PaneIds.Comments))
        {
            return null;
        }

        // Comments is the last pane of its column only beside the sidebar; under it (narrow) the Details pane follows,
        // so the keys that would run past the last comment move on to it (G still lands on the last comment).
        var last = document.SectionIds[^1] == PaneIds.Comments;

        var rows = DetailZoom.CommentRowsOf(top, document.Width);
        var stops = Enumerable.Range(0, rows.Count).Where(index => rows[index].IsStop).ToArray();
        var current = row ?? -1;
        var page = Math.Max(1, (CommentsViewport(document) ?? rows.Count) / 2);

        if (stops.Length == 0)
        {
            return null;
        }

        var target = move switch
        {
            ListMove.Down => stops.FirstOrDefault(stop => stop > current, -1),
            ListMove.Up => stops.LastOrDefault(stop => stop < current, -1),
            ListMove.PageDown => stops.FirstOrDefault(stop => stop >= current + page, stops[^1]),
            ListMove.PageUp => stops.LastOrDefault(stop => stop <= current - page, stops[0]),
            _ => stops[^1],
        };

        if (target < 0)
        {
            return move == ListMove.Up || !last ? null : state;
        }

        if (target == current)
        {
            return move is ListMove.PageUp || !last ? null : state;
        }

        var ahead = move is ListMove.Down or ListMove.PageDown or ListMove.Bottom;
        var placed = PlaceInComments(top, target, DetailSize.ListRows(state, tab), ahead, rows);

        return ViewReducer.Replace(state, ReplaceTop(tab, placed));
    }

    /// <summary>
    /// The row of the Comments pane's content the cursor is on, null when the cursor is elsewhere or there is no
    /// pane.
    /// </summary>
    private static int? CommentsRowOf(DetailState detail)
    {
        if (detail.Document is not { } document)
        {
            return null;
        }

        var section = document.SectionIds.ToList().IndexOf(PaneIds.Comments);

        if (section < 0 || document.SectionAt(detail.Cursor) != section || detail.Cursor <= document.PaneStarts[section])
        {
            return null;
        }

        var offset = document.PaneScrolls[section]?.ClampedOffset ?? 0;

        return offset + detail.Cursor - document.Sections[section];
    }

    /// <summary>
    /// The row the cursor goes to after the comment list changed: the same line of the same comment as before, so a
    /// comment added above, one removed or one that grew does not move the cursor onto another comment. The old row
    /// stands when the comment is gone.
    /// </summary>
    private static int? FollowedCommentsRow(DetailState detail, int width)
    {
        var row = CommentsRowOf(detail);

        if (row is not { } old || detail.Document is not { } document || old >= document.CommentRowIds.Count
            || document.CommentRowIds[old] is not { } id)
        {
            return row;
        }

        var offset = old - document.CommentRowIds.ToList().IndexOf(id);
        var rows = DetailZoom.CommentRowsOf(detail, width);
        var header = CommentRows.HeaderOf(rows, id);

        return header < 0 ? row : Math.Min(header + offset, CommentRows.EndOf(rows, header));
    }

    private static int? CommentsViewport(DetailDocument document)
    {
        var section = document.SectionIds.ToList().IndexOf(PaneIds.Comments);

        return section < 0 ? null : document.PaneScrolls[section]?.Viewport;
    }

    /// <summary>
    /// Puts the cursor on the stop at or after <paramref name="row"/> of the Comments pane's content (the last stop
    /// when there is none) and scrolls the pane the least that shows it; <paramref name="ahead"/> shows the rest of
    /// the comment too when it fits.
    /// </summary>
    private static DetailState PlaceInComments(
        DetailState detail,
        int row,
        int listRows,
        bool ahead,
        IReadOnlyList<CommentRow>? known = null)
    {
        if (detail.Document is not { } document)
        {
            return detail;
        }

        var section = document.SectionIds.ToList().IndexOf(PaneIds.Comments);
        var rows = known ?? DetailZoom.CommentRowsOf(detail, document.Width);
        var stop = NearestStop(rows, row);

        if (section < 0 || stop < 0)
        {
            return detail;
        }

        var viewport = document.PaneScrolls[section]?.Viewport ?? rows.Count;
        var scroll = new PaneScroll(detail.CommentsOffset, rows.Count, viewport);
        var revealed = ahead ? scroll.Reveal(CommentRows.EndOf(rows, stop)).Reveal(stop) : scroll.Reveal(stop);
        var moved = detail with { CommentsOffset = revealed.ClampedOffset };
        var rebuilt = IssueDocumentBuilder.Build(moved, document.Width, document.ViewportRows, rows);
        var first = rebuilt.Sections[section];
        var shown = rebuilt.PaneScrolls[section]?.ClampedOffset ?? 0;
        var laidOut = moved with { Document = rebuilt, Cursor = first + stop - shown };

        return WithCursor(laidOut, laidOut.Cursor, DetailLayout.ViewportRows(listRows));
    }

    private static int NearestStop(IReadOnlyList<CommentRow> rows, int row)
    {
        var last = -1;

        for (var index = 0; index < rows.Count; index++)
        {
            if (!rows[index].IsStop)
            {
                continue;
            }

            last = index;

            if (index >= row)
            {
                return index;
            }
        }

        return last;
    }

    /// <summary>The row of the header of the comment <paramref name="target"/> names, null for any other target.</summary>
    private static int? HeaderRowOf(DetailState detail, DetailTarget target, int columns)
    {
        if (target.Kind != DetailTargetKind.Comment)
        {
            return null;
        }

        var width = columns > 0 ? columns : DetailLayout.DefaultWidth;
        var rows = DetailZoom.CommentRowsOf(detail, width);
        var header = CommentRows.HeaderOf(rows, target.Value);

        return header < 0 ? null : header;
    }
}
