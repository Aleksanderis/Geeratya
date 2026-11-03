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

    /// <summary>The cells of the commands chip at the right end of the tab bar row, null when the bar draws none.</summary>
    public TabButton? CommandsChip { get; init; }

    /// <summary>The cells of the jump chip at the right end of the tab bar row, null when the bar draws none.</summary>
    public TabButton? JumpChip { get; init; }

    /// <summary>The cells of the <c>Lane n/N: Name</c> text in the status line, null when it is not drawn whole.</summary>
    public TabButton? LaneChip { get; init; }

    /// <summary>The banner row shows a line (a click there refreshes).</summary>
    public bool BannerShown { get; init; }

    /// <summary>The clickable rectangles of the open overlay as drawn, later ones on top; may be empty.</summary>
    public IReadOnlyList<OverlayRegion> Regions { get; init; } = [];

    /// <summary>The cells of the status message in the status line, null when none shows.</summary>
    public TabButton? StatusMessage { get; init; }

    /// <summary>The active tab shows the columns layout, so a drag moves a card; otherwise it selects rows.</summary>
    public bool ColumnsLayout { get; init; }

    /// <summary>The Confirm dialog is open: a click outside its box, the status line included, cancels.</summary>
    public bool ConfirmOpen { get; init; }

    /// <summary>
    /// The overlay a wheel notch scrolls or moves wherever the pointer is (help, messages, context menu), null when the
    /// open overlay (if any) does not take the wheel.
    /// </summary>
    public MouseTarget? WheelTarget { get; init; }

    public static MouseContext From(
        AppState state,
        int width,
        int height,
        int headerRows,
        DateTimeOffset now,
        IReadOnlyList<OverlayRegion>? regions = null,
        KeymapTable? keymap = null)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);
        var overlay = state.Mode != InputMode.Normal || state.Picker is not null || state.Jump is not null
            || state.Editor is not null || state.ProfileList is not null || state.SettingsScreen is not null
            || FormReducer.IsOpen(state);

        return new MouseContext(width, height, headerRows, overlay, tab is { Detail.Count: > 0 })
        {
            TabAreas = TabBar.Areas(state.Session.Tabs, state.Session.Active, width, keymap, state.HoveredTab),
            NewTabButton = TabBar.NewTabButton(state.Session.Tabs, state.Session.Active, width, keymap, state.HoveredTab),
            CommandsChip = TabBar.CommandsChip(state.Session.Tabs, state.Session.Active, width, keymap, state.HoveredTab),
            JumpChip = TabBar.JumpChip(state.Session.Tabs, state.Session.Active, width, keymap, state.HoveredTab),
            StatusMessage = StatusMessages.Cells(state),
            BannerShown = NetworkBanner.Line(state, now, width) is not null,
            Regions = regions ?? [],
            ColumnsLayout = tab?.Definition.Layout == ViewLayout.Columns,
            WheelTarget = WheelTargetOf(state),
            ConfirmOpen = state.Mode == InputMode.Confirm && state.Confirm is not null,
        };
    }

    private static MouseTarget? WheelTargetOf(AppState state)
    {
        if (state.ContextMenu is not null)
        {
            return MouseTarget.ContextMenu;
        }

        if (state.Mode != InputMode.Help)
        {
            return null;
        }

        return state.MessagesOpen ? MouseTarget.MessagesBody : MouseTarget.HelpBody;
    }
}
