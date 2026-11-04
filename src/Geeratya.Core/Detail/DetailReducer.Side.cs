using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

internal static partial class DetailReducer
{
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
