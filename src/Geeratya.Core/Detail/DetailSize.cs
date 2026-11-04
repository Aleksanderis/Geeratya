using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// The size a detail document is laid out for: the width and height of the overlay it is drawn in, not the whole
/// body. Before the host reports a viewport the list's own numbers stand in.
/// </summary>
public static class DetailSize
{
    /// <summary>The layout width for the viewport of <paramref name="state"/>.</summary>
    public static int Columns(AppState state)
    {
        return Columns(state.ViewportColumns, state.ViewportBodyHeight);
    }

    /// <summary>The layout width for <paramref name="columns"/> terminal columns and <paramref name="bodyHeight"/> body rows.</summary>
    public static int Columns(int columns, int bodyHeight)
    {
        return columns <= 0 || bodyHeight < 0 ? columns : DetailOverlayGeometry.Place(columns, bodyHeight).Width;
    }

    /// <summary>The list rows (<see cref="TabState.VisibleRows"/> terms) that give the overlay its rows.</summary>
    public static int ListRows(AppState state, TabState tab)
    {
        return ForTab(tab, ListRows(state.ViewportColumns, state.ViewportBodyHeight, tab.VisibleRows));
    }

    /// <summary>
    /// <paramref name="rows"/> less the section strip when the top issue of <paramref name="tab"/> shows a zoomed pane.
    /// </summary>
    public static int ForTab(TabState tab, int rows)
    {
        return tab.Detail is [.., { Zoomed: not null }] ? rows - DetailZoom.StripRows : rows;
    }

    /// <summary>The list rows for a viewport; <paramref name="fallback"/> while the body height is unknown.</summary>
    public static int ListRows(int columns, int bodyHeight, int fallback)
    {
        if (columns <= 0 || bodyHeight < 0)
        {
            return fallback;
        }

        var box = DetailOverlayGeometry.Place(columns, bodyHeight);
        var documentRows = box.Height - DetailLayout.HeaderRows - DetailLayout.FooterRows;

        // DetailLayout.ViewportRows maps list rows to document rows; this is its inverse.
        return documentRows - ListLayout.HeaderRows - ListLayout.FooterRows + DetailLayout.HeaderRows
            + DetailLayout.FooterRows;
    }
}
