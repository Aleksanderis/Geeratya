using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// A cursor move in a zoomed pane that scrolls inside its frame (every zoomed pane but Comments, #740): the move
    /// is made over the whole pane and the window follows the cursor, showing the whole field it is on. Null when the
    /// pane is not zoomed, fits its frame or the Comments pane is zoomed (that has its own scroll).
    /// </summary>
    private static AppState? MoveInZoomedPane(AppState state, TabState tab, ListMove move, int viewport)
    {
        var top = tab.Detail[^1];

        if (top.Zoomed is null or PaneIds.Comments || top.SideCursor is not null
            || top.Document is not { PaneScrolls: [{ Scrollable: true } scroll] } document
            || document.ViewportRows is not { } rows)
        {
            return null;
        }

        var whole = IssueDocumentBuilder.Build(top with { ZoomOffset = 0 }, document.Width);
        var offset = scroll.ClampedOffset;
        var section = whole.Sections[0];
        var target = Target(whole, top.Cursor + offset, move, viewport);
        var end = (whole.BlockAt(target)?.End ?? target + 1) - 1;
        var revealed = scroll.Reveal(end - section).Reveal(target - section);
        var moved = top with { ZoomOffset = revealed.ClampedOffset };
        var rebuilt = IssueDocumentBuilder.Build(moved, document.Width, rows);
        var cursor = rebuilt.Snap(target - revealed.ClampedOffset);

        return ViewReducer.Replace(state, ReplaceTop(tab, moved with { Document = rebuilt, Cursor = cursor }));
    }
}
