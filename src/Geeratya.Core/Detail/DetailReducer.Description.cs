using Geeratya.Core.Lists;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// <c>j</c>/<c>k</c> at the edge of a cut Description scroll it by a line instead of leaving it: <c>j</c> on the
    /// last text row shows one more line, <c>k</c> on the first shows the one above. Null when the cursor is not in
    /// a cut Description, is not on that edge or the scroll is at its end, so the cursor moves as usual (onto the
    /// note, out of the pane).
    /// </summary>
    private static AppState? MoveInDescription(AppState state, TabState tab, ListMove move)
    {
        var top = tab.Detail[^1];

        if (move is not (ListMove.Down or ListMove.Up) || top.Document is not { } document)
        {
            return null;
        }

        var section = document.SectionIds.ToList().IndexOf(PaneIds.Description);

        if (section < 0 || document.SectionAt(top.Cursor) != section || document.PaneScrolls[section] is not { } scroll)
        {
            return null;
        }

        var row = top.Cursor - document.Sections[section];
        var by = move == ListMove.Down && row == scroll.Viewport - 1 ? 1 : move == ListMove.Up && row == 0 ? -1 : 0;
        var scrolled = scroll.By(by);

        if (by == 0 || scrolled.ClampedOffset == scroll.ClampedOffset)
        {
            return null;
        }

        var moved = top with { DescriptionOffset = scrolled.ClampedOffset };
        var rebuilt = IssueDocumentBuilder.Build(moved, document.Width, document.ViewportRows);

        return ViewReducer.Replace(state, ReplaceTop(tab, moved with { Document = rebuilt }));
    }
}
