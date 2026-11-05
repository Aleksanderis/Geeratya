using Geeratya.Core.Detail;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The wide overlay that hosts the issue detail (<c>docs/ux-screens.md</c>, "Where it is drawn"): the view of the tab
/// stays behind it, muted, and the detail is drawn over it at <see cref="DetailOverlayGeometry"/>. The panes sit
/// in a card (<see cref="DetailCard"/>). Below 100 columns it fills the body and the view is not drawn.
/// </summary>
public static class DetailOverlay
{
    private static readonly IssueDetailRenderer _detail = new();

    /// <summary>An issue is open on the active tab, so the overlay is drawn.</summary>
    public static bool Applies(AppState state)
    {
        return _detail.AppliesTo(state);
    }

    /// <summary>The state of the view behind the overlay: the active tab without its issue stack.</summary>
    public static AppState Underlying(AppState state)
    {
        if (state.Session.Active is not { } active)
        {
            return state;
        }

        var tabs = state.Session.Tabs
            .Select(tab => tab.Id == active && tab.Detail.Count > 0 ? tab with { Detail = [] } : tab);

        return state with { Session = state.Session with { Tabs = [.. tabs] } };
    }

    /// <summary>Draws the detail of <paramref name="state"/> over <paramref name="body"/>, the view behind it.</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        BodyContext context,
        int width,
        int height)
    {
        var box = DetailOverlayGeometry.Place(width, height);
        var lines = _detail.Render(state, context, box.Width, box.Height);

        if (box.Fills)
        {
            return lines;
        }

        return DetailCard.Draw(body, lines, box, width);
    }
}
