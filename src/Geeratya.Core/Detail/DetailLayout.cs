using Geeratya.Core.Lists;

namespace Geeratya.Core.Detail;

/// <summary>Fixed measures of the issue detail screen that the host and the renderer must agree on.</summary>
public static class DetailLayout
{
    /// <summary>Rows above the scrolling document: the top edge of the frame, with the title of the pane in view.</summary>
    public const int HeaderRows = 1;

    /// <summary>Rows under the scrolling document: the bottom edge of the frame.</summary>
    public const int FooterRows = 1;

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
