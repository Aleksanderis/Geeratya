namespace Geeratya.Core.Detail;

/// <summary>
/// Which lines of the Details sidebar are shown: its top and bottom edges stay, the content between them scrolls so
/// that the field under the cursor is visible. Shared by the reducer (clicks) and the renderer. Pure.
/// </summary>
public static class SideView
{
    private const int EdgeRows = 2;

    /// <summary>
    /// The offset added to a body row to get the sidebar line drawn there (row 0 is the top edge): 0 when everything
    /// fits, else the scroll that keeps the cursor field visible.
    /// </summary>
    public static int FirstLine(DetailDocument side, int? cursor, int rows)
    {
        var visible = Math.Max(0, rows - EdgeRows);
        var content = Math.Max(0, side.Lines.Count - EdgeRows);

        if (content <= visible || cursor is not { } line)
        {
            return 0;
        }

        var end = side.BlockAt(line)?.End ?? line + 1;

        return Math.Clamp(end - 1 - visible, 0, content - visible);
    }
}
