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
    /// Document rows the detail can show, from the list's row count (<see cref="Views.TabState.VisibleRows"/> is the
    /// body height without the list's own header rows).
    /// </summary>
    public static int ViewportRows(int listRows)
    {
        return Math.Max(1, listRows + ListLayout.HeaderRows + ListLayout.FooterRows - HeaderRows - FooterRows);
    }
}
