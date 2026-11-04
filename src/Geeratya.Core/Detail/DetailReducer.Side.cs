using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
    /// <summary>
    /// A click on the Details sidebar: the sidebar takes the focus and its cursor goes to the field under the pointer.
    /// Null when the click is not on the sidebar, so the main column handles it.
    /// </summary>
    private static AppState? SidePlaced(AppState state, TabState tab, int bodyRow, int bodyColumn)
    {
        var top = tab.Detail[^1];

        if (top.Document is not { Side: { } side } document
            || bodyColumn < DetailLayout.MainWidth(document.Width) + DetailLayout.SidebarGap)
        {
            return null;
        }

        var rows = DetailLayout.ViewportRows(DetailSize.ListRows(state, tab));
        var first = SideView.FirstLine(side, top.SideCursor, rows);

        if (bodyRow < 1 || bodyRow >= rows - 1 || first + bodyRow >= side.Lines.Count - 1)
        {
            return state;
        }

        var cursor = side.Snap(first + bodyRow);

        return ViewReducer.Replace(state, ReplaceTop(tab, top with { SideCursor = cursor }));
    }

    /// <summary>
    /// The sidebar cursor after a new layout: still on the same field, else clamped; null while the focus is on the
    /// main column.
    /// </summary>
    private static int? SideAnchored(DetailState detail, DetailDocument side)
    {
        if (detail.SideCursor is not { } cursor)
        {
            return null;
        }

        var old = detail.Document?.Side;

        if (old is not null && cursor >= 0 && cursor < old.Targets.Count && old.Targets[cursor] is { } target)
        {
            var found = side.Targets.ToList().FindIndex(candidate => Equals(candidate, target));

            if (found >= 0)
            {
                return side.Snap(found);
            }
        }

        return side.Snap(Math.Clamp(cursor, 0, Math.Max(0, side.Lines.Count - 1)));
    }

    /// <summary>
    /// Moves the focus between the main column and the Details sidebar. Returns the state unchanged when the move has
    /// no target (no sidebar, or the focus is already there).
    /// </summary>
    public static AppState FocusSide(AppState state, TabState tab, bool side)
    {
        var top = tab.Detail[^1];

        if (top.Document is not { Side: { } sideDocument } || (top.SideCursor is not null) == side)
        {
            return state;
        }

        int? cursor = side ? sideDocument.Snap(0) : null;

        return ViewReducer.Replace(state, ReplaceTop(tab, top with { SideCursor = cursor }));
    }
}
