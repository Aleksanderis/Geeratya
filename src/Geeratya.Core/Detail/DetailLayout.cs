using Geeratya.Core.Lists;

namespace Geeratya.Core.Detail;

/// <summary>Fixed measures of the issue detail screen that the host and the renderer must agree on.</summary>
public static class DetailLayout
{
    /// <summary>Rows above the scrolling document: none, every pane carries its own top edge.</summary>
    public const int HeaderRows = 0;

    /// <summary>Rows under the scrolling document: none, every pane carries its own bottom edge.</summary>
    public const int FooterRows = 0;

    /// <summary>Width used before the host has reported one.</summary>
    public const int DefaultWidth = 80;

    /// <summary>
    /// Overlay widths from this up show the Details pane as a sidebar; below it the pane is under the others.
    /// </summary>
    public const int SidebarMinWidth = 100;

    /// <summary>The width of the Details sidebar, frame included.</summary>
    public const int SidebarWidth = 33;

    /// <summary>The columns between the main column and the sidebar.</summary>
    public const int SidebarGap = 1;

    /// <summary>Whether a detail <paramref name="width"/> columns wide shows the Details pane as a sidebar.</summary>
    public static bool HasSidebar(int width)
    {
        return width >= SidebarMinWidth;
    }

    /// <summary>The width of the column the main panes are laid out for: all of it, or less the sidebar.</summary>
    public static int ColumnWidth(int width)
    {
        return HasSidebar(width) ? MainWidth(width) : width;
    }

    /// <summary>
    /// The width the panes of <paramref name="detail"/> are laid out for in a document <paramref name="width"/> columns
    /// wide: all of it when a pane is zoomed (no sidebar), else <see cref="ColumnWidth"/>.
    /// </summary>
    public static int PaneWidth(DetailState detail, int width)
    {
        return detail.Zoomed is not null ? width : ColumnWidth(width);
    }

    /// <summary>
    /// The width of the main column for a detail <paramref name="width"/> columns wide with a sidebar.
    /// </summary>
    public static int MainWidth(int width)
    {
        return width - SidebarWidth - SidebarGap;
    }

    /// <summary>
    /// Document rows the detail can show, from the list's row count (<see cref="Views.TabState.VisibleRows"/> is the
    /// body height without the list's own header rows).
    /// </summary>
    public static int ViewportRows(int listRows)
    {
        return Math.Max(1, listRows + ListLayout.HeaderRows + ListLayout.FooterRows - HeaderRows - FooterRows);
    }
}
