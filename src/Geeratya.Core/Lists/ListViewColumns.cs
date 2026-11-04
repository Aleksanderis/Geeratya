using Geeratya.Core.Views;

namespace Geeratya.Core.Lists;

/// <summary>The column widths a list tab is drawn with; the renderer and the flash jump read the same numbers.</summary>
public static class ListViewColumns
{
    /// <summary>Narrowest body that gets a frame (<c>FrameGrid.MinFramedWidth</c>, kept equal by a test).</summary>
    public const int MinFramedWidth = 20;

    /// <summary>Shortest body that gets a frame (<c>FrameGrid.MinFramedHeight</c>, kept equal by a test).</summary>
    public const int MinFramedHeight = 5;

    /// <summary>The width the rows of a body of this size are drawn in: inside the frame when there is one.</summary>
    /// <param name="width">The body width.</param>
    /// <param name="height">The body height, negative when not reported yet (a frame is assumed).</param>
    public static int InnerWidth(int width, int height)
    {
        return IsFramed(width, height) ? width - 2 : width;
    }

    /// <summary>Whether a body of this size gets a frame (a negative height, not reported yet, assumes one).</summary>
    public static bool IsFramed(int width, int height)
    {
        return width >= MinFramedWidth && (height < 0 || height >= MinFramedHeight);
    }

    /// <summary>
    /// The columns of a Scrum backlog: the optional ones follow the whole backlog, not the window, so they do not jump
    /// while scrolling, and the sprint column is left out because the section header says it.
    /// </summary>
    public static ListColumns ForBacklog(TabState tab, int innerWidth)
    {
        var points = tab.Rows.Any(row => row.Estimate is not null);
        var epic = tab.Rows.Any(row => row.Epic is not null);

        return ListColumns.For(innerWidth, tab.KeyWidth, false, points, epic);
    }

    /// <summary>
    /// The columns for <paramref name="innerWidth"/>: the sprint, estimate and epic columns show only while one of
    /// the <paramref name="area"/> rows from the scroll position has a value.
    /// </summary>
    public static ListColumns For(TabState tab, int innerWidth, int area)
    {
        bool sprint = false, points = false, epic = false;
        var first = Math.Clamp(tab.Scroll, 0, Math.Max(0, tab.Rows.Count - 1));
        var end = Math.Min(tab.Rows.Count, first + area);

        for (var index = first; index < end; index++)
        {
            var row = tab.Rows[index];
            sprint |= row.CurrentSprint is not null;
            points |= row.Estimate is not null;
            epic |= row.Epic is not null;
        }

        return ListColumns.For(innerWidth, tab.KeyWidth, sprint, points, epic);
    }
}
