using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    private const string NothingToZoomText = "Nothing to zoom in this pane.";

    /// <summary>
    /// <c>pane.zoom</c>: zooms the pane that holds the cursor, or leaves the zoom. The title pane has nothing to show
    /// larger and says so.
    /// </summary>
    public static AppState ToggleZoom(AppState state, TabState tab)
    {
        var top = tab.Detail[^1];

        if (top.Zoomed is not null)
        {
            return Reshow(state, tab, null, true);
        }

        if (top.Document is not { SectionIds.Count: > 0 } document)
        {
            return state;
        }

        var onSide = top.SideCursor is not null && document.Side is not null;
        var pane = onSide ? PaneIds.Details : document.SectionIds[document.SectionAt(top.Cursor)];

        if (pane is PaneIds.Title or PaneIds.Error)
        {
            return state with { Message = NothingToZoomText };
        }

        return Reshow(state, tab, pane, true);
    }

    /// <summary>Zooms the pane <paramref name="pane"/> (the Attachments row of the Details pane does).</summary>
    public static AppState ZoomPane(AppState state, TabState tab, string pane)
    {
        return Reshow(state, tab, pane, true);
    }

    /// <summary>
    /// Leaves the zoom of the top issue of <paramref name="tab"/>; null when it is not zoomed (<c>Esc</c> then pops
    /// the issue).
    /// </summary>
    public static AppState? LeaveZoom(AppState state, TabState tab)
    {
        return tab.Detail is [.., { Zoomed: not null }] ? Reshow(state, tab, null, true) : null;
    }

    /// <summary>
    /// <c>Ctrl-h</c>/<c>Ctrl-l</c> while zoomed: the previous or next section of the strip (it does not wrap).
    /// </summary>
    public static AppState SwitchSection(AppState state, TabState tab, int step)
    {
        var top = tab.Detail[^1];

        if (top.Document is not { IsZoomed: true } document)
        {
            return state;
        }

        var ids = document.Strip.Select(static section => section.Id).ToList();
        var next = Math.Clamp(ids.IndexOf(top.Zoomed ?? string.Empty) + step, 0, ids.Count - 1);

        return ids[next] == top.Zoomed ? state : Reshow(state, tab, ids[next], false);
    }

    // Lays the top issue out zoomed to pane (null: all panes) and puts the cursor back on the row it was on, when the
    // new layout shows it; else on the first row of the pane.
    private static AppState Reshow(AppState state, TabState tab, string? pane, bool keepTarget)
    {
        var top = tab.Detail[^1];
        var leaving = top.Zoomed;
        var target = keepTarget ? TargetOf(top) : null;
        var changed = top with { Zoomed = pane, SideCursor = null, Cursor = 0, Scroll = 0, ZoomOffset = 0 };
        var shown = ReplaceTop(tab, changed);
        var columns = DetailSize.Columns(state);
        var rows = DetailSize.ListRows(state, shown);
        var header = target is null ? null : HeaderRowOf(changed, target, columns);
        var laidOut = Layout(changed, columns, rows, header);
        var placed = Placed(laidOut, target, leaving, DetailLayout.ViewportRows(rows));

        return ViewReducer.Replace(state, ReplaceTop(shown, placed));
    }

    private static DetailState Placed(DetailState detail, DetailTarget? target, string? leaving, int viewport)
    {
        if (detail.Document is not { } document || document.Lines.Count == 0)
        {
            return detail;
        }

        if (target is not null && detail.Zoomed is null && leaving == PaneIds.Details && document.Side is { } side)
        {
            var at = side.Targets.ToList().FindIndex(candidate => Equals(candidate, target));

            return detail with { SideCursor = side.Snap(Math.Max(0, at)) };
        }

        if (target is { Kind: DetailTargetKind.Comment })
        {
            return detail;
        }

        var line = target is null ? -1 : document.Targets.ToList().FindIndex(candidate => Equals(candidate, target));

        if (line >= 0)
        {
            return WithCursor(detail, line, viewport);
        }

        var pane = leaving is null ? -1 : document.SectionIds.ToList().IndexOf(leaving);

        if (pane < 0 && detail.Zoomed is not null)
        {
            pane = 0;
        }

        if (pane < 0)
        {
            return detail;
        }

        var cursor = document.Snap(document.Sections[pane]);
        var scroll = Math.Clamp(document.PaneStarts[pane], 0, Math.Max(0, document.Lines.Count - viewport));

        return detail with { Cursor = cursor, Scroll = scroll };
    }
}
