using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Mouse;

/// <summary>What the hit test needs to know about the screen: its size and what covers the body.</summary>
/// <param name="Width">Terminal width in cells.</param>
/// <param name="Height">Terminal height in rows.</param>
/// <param name="HeaderRows">Rows above the body (tab bar and banner).</param>
/// <param name="OverlayOpen">A prompt, picker, form, editor, help or confirm covers the body.</param>
/// <param name="DetailOpen">An issue is opened in the active tab.</param>
public sealed record MouseContext(int Width, int Height, int HeaderRows, bool OverlayOpen, bool DetailOpen)
{
    /// <summary>The cells of each tab in the tab bar row.</summary>
    public IReadOnlyList<TabArea> TabAreas { get; init; } = [];

    /// <summary>The cells of the <c>+</c> button after the last tab, null when the bar draws none.</summary>
    public TabButton? NewTabButton { get; init; }

    /// <summary>The banner row shows a line (a click there refreshes).</summary>
    public bool BannerShown { get; init; }

    /// <summary>The clickable rectangles of the open overlay as drawn, later ones on top; may be empty.</summary>
    public IReadOnlyList<OverlayRegion> Regions { get; init; } = [];

    /// <summary>The cells of the status message in the status line, null when none shows.</summary>
    public TabButton? StatusMessage { get; init; }

    /// <summary>The Confirm dialog is open: a click outside its box, the status line included, cancels.</summary>
    public bool ConfirmOpen { get; init; }

    public static MouseContext From(
        AppState state,
        int width,
        int height,
        int headerRows,
        DateTimeOffset now,
        IReadOnlyList<OverlayRegion>? regions = null)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);
        var overlay = state.Mode != InputMode.Normal || state.Picker is not null || state.Jump is not null
            || state.Editor is not null || state.ProfileList is not null || FormReducer.IsOpen(state);

        return new MouseContext(width, height, headerRows, overlay, tab is { Detail.Count: > 0 })
        {
            TabAreas = TabBar.Areas(state.Session.Tabs, state.Session.Active, width),
            NewTabButton = TabBar.NewTabButton(state.Session.Tabs, state.Session.Active, width),
            StatusMessage = StatusMessages.Cells(state),
            BannerShown = NetworkBanner.Line(state, now, width) is not null,
            Regions = regions ?? [],
            ConfirmOpen = state.Mode == InputMode.Confirm && state.Confirm is not null,
        };
    }
}
