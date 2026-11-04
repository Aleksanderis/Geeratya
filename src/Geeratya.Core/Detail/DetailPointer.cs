using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>What a pointer cell of the detail overlay is on.</summary>
internal enum DetailPointerKind
{
    /// <summary>Nothing: outside the document or on a separator of the strip.</summary>
    None,

    /// <summary>A section chip of the zoom strip (<see cref="DetailPointerHit.Section"/>).</summary>
    Strip,

    /// <summary>A row of a pane (<see cref="DetailPointerHit.Line"/> in the main column or the sidebar).</summary>
    Row,

    /// <summary>
    /// The frame of a pane or the empty space inside the sidebar: the pane takes the focus, on
    /// <see cref="DetailPointerHit.Line"/> when it is set, else on its current or first row.
    /// </summary>
    Frame,
}

/// <summary>The detail element under a pointer cell.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="OnSide">Whether the cell is in the Details sidebar.</param>
/// <param name="Line">The document line (of the sidebar when <paramref name="OnSide"/>), -1 when there is none.</param>
/// <param name="Section">The pane id of a strip chip.</param>
internal sealed record DetailPointerHit(DetailPointerKind Kind, bool OnSide, int Line, string? Section = null);

/// <summary>
/// Hit test of the issue detail overlay (<c>docs/ux-mouse.md</c>, "Detail targets"): which strip chip, pane row or
/// pane frame lies under a cell of the overlay, using the same measures the renderer draws with. Pure.
/// </summary>
internal static class DetailPointer
{
    private static readonly DetailPointerHit _nothing = new(DetailPointerKind.None, false, -1);

    /// <summary>
    /// The element of the top issue of <paramref name="tab"/> at overlay cell (<paramref name="bodyColumn"/>,
    /// <paramref name="bodyRow"/>), the first row of the overlay being row 0.
    /// </summary>
    public static DetailPointerHit Locate(AppState state, TabState tab, int bodyRow, int bodyColumn)
    {
        var top = tab.Detail[^1];

        if (top.Document is not { } document || bodyRow < 0)
        {
            return _nothing;
        }

        // The layout of the document (width, sidebar split) is the one the last layout produced; a click in the one
        // frame between a resize and its re-layout resolves against it.
        var rows = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));
        var strip = top.Zoomed is null ? 0 : DetailZoom.StripRows;

        if (bodyRow < strip)
        {
            return DetailStrip.SectionAt(document, top.Zoomed, document.Width, bodyColumn) is { } section
                ? new DetailPointerHit(DetailPointerKind.Strip, false, -1, section)
                : _nothing;
        }

        if (document.Side is { } side && bodyColumn >= DetailLayout.MainWidth(document.Width) + DetailLayout.SidebarGap)
        {
            return OnSidebar(top, side, rows, bodyRow);
        }

        var line = top.Scroll + bodyRow - DetailLayout.HeaderRows - strip;

        if (bodyRow - strip >= rows || line >= document.Lines.Count)
        {
            return _nothing;
        }

        if (document.PaneStarts.Contains(line))
        {
            return new DetailPointerHit(DetailPointerKind.Frame, false, line);
        }

        return IsBottomEdge(document, line)
            ? new DetailPointerHit(DetailPointerKind.Frame, false, document.StopBefore(line))
            : new DetailPointerHit(DetailPointerKind.Row, false, line);
    }

    /// <summary>
    /// Whether a click on <paramref name="hit"/> acts on the row it placed the cursor on, like <c>Enter</c> would (a
    /// related issue opens, a cut text expands, an editable Details field opens its dropdown by its value). A
    /// double-click does the same on any row.
    /// </summary>
    public static bool OpensOnClick(DetailState top, DetailPointerHit hit)
    {
        if (hit.Kind != DetailPointerKind.Row || top.Document is not { } document)
        {
            return false;
        }

        var shown = hit.OnSide ? document.Side : document;

        if (shown is null || hit.Line < 0 || hit.Line >= shown.Targets.Count
            || shown.Targets[hit.Line] is not { } target)
        {
            return false;
        }

        if ((target.Toggles && target.Note) || target.Kind == DetailTargetKind.Issue)
        {
            return true;
        }

        var label = shown.BlockAt(hit.Line)?.Start ?? hit.Line;

        return hit.OnSide && target is { Kind: DetailTargetKind.Field } && hit.Line > label
            && DetailsPane.IsEditable(top, target.Value);
    }

    private static DetailPointerHit OnSidebar(DetailState top, DetailDocument side, int rows, int bodyRow)
    {
        var first = SideView.FirstLine(side, top.SideCursor, rows);
        var line = first + bodyRow;
        var inside = bodyRow >= 1 && bodyRow < rows - 1 && line < side.Lines.Count - 1;

        return inside
            ? new DetailPointerHit(DetailPointerKind.Row, true, line)
            : new DetailPointerHit(DetailPointerKind.Frame, true, -1);
    }

    // The last line of a pane is its bottom edge, which is no stop: a click there belongs to the pane above it.
    private static bool IsBottomEdge(DetailDocument document, int line)
    {
        if (document.Stops.Contains(line) || document.BlockAt(line) is not null)
        {
            return false;
        }

        var next = document.PaneStarts.FirstOrDefault(start => start > line, document.Lines.Count);

        return line == next - 1;
    }
}
