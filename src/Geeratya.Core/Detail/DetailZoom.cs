namespace Geeratya.Core.Detail;

/// <summary>
/// Measures of the zoomed pane of the issue detail (<c>docs/internal/ux-screens.md</c>, "Panes, focus and zoom"): the section
/// strip takes the first row of the overlay, the zoomed pane the rest, and a comment is cut after 6 lines instead of 3.
/// </summary>
public static class DetailZoom
{
    /// <summary>Rows the section strip takes at the top of the overlay.</summary>
    public const int StripRows = 1;

    /// <summary>Body lines of a comment before the cut when the Comments pane is zoomed.</summary>
    public const int ZoomedBodyLines = 6;

    /// <summary>The body lines a comment is cut after, for the pane layout of <paramref name="detail"/>.</summary>
    public static int BodyLines(DetailState detail)
    {
        return detail.Zoomed == PaneIds.Comments ? ZoomedBodyLines : CommentRows.BodyLines;
    }

    /// <summary>The rows of the Comments pane for a document <paramref name="width"/> columns wide.</summary>
    public static IReadOnlyList<CommentRow> CommentRowsOf(DetailState detail, int width)
    {
        var inner = Panel.InnerWidth(DetailLayout.PaneWidth(detail, width));

        return detail.Render.Rows(detail, inner, BodyLines(detail));
    }
}
