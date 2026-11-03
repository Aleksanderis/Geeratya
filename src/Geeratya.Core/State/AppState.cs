using Geeratya.Core.Comments;
using Geeratya.Core.Jump;
using Geeratya.Core.Menus;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;

namespace Geeratya.Core.State;

/// <summary>Everything the shell shows; a new value comes out of every command and nothing mutates it.</summary>
/// <param name="Mode">The current input mode.</param>
/// <param name="PendingKeys">Keys typed so far of a multi-key binding such as <c>g g</c>, empty otherwise.</param>
/// <param name="PaletteText">The text typed after <c>:</c> in Command mode.</param>
/// <param name="HelpScroll">The first visible line of the help overlay.</param>
/// <param name="Message">A one-line notice for the status line, or null.</param>
/// <param name="Confirm">The open confirmation in Confirm mode, otherwise null.</param>
/// <param name="Profiles">The saved profiles and the active one.</param>
/// <param name="Form">The add-profile form in Insert mode, otherwise null.</param>
/// <param name="Session">The tabs and link state of the active profile's site; replaced on a profile change.</param>
/// <param name="ProfileBusy">A profile change is being saved; further changes are refused until it ends.</param>
/// <param name="Sequence">The counter every tab id, request id and recency stamp is drawn from; it only grows.</param>
/// <param name="ViewportRows">The last viewport height the host reported; new tabs start with it.</param>
/// <param name="Picker">The open source picker (Insert mode), otherwise null.</param>
public sealed record AppState(
    InputMode Mode,
    string PendingKeys,
    string PaletteText,
    int HelpScroll,
    string? Message,
    ConfirmRequest? Confirm,
    ProfileCatalog Profiles,
    ProfileForm? Form,
    bool ProfileBusy,
    SiteSession Session,
    long Sequence,
    int ViewportRows,
    SourcePickerState? Picker = null)
{
    /// <summary>The last terminal width the host reported; issue details are laid out for it (0 until known).</summary>
    public int ViewportColumns { get; init; }

    /// <summary>
    /// The rows between the header and the status line, as the frame draws the body (what overlays such as the
    /// Confirm dialog are placed in); <see cref="UnknownBodyHeight"/> until the host reports it.
    /// </summary>
    public int ViewportBodyHeight { get; init; } = UnknownBodyHeight;

    public const int UnknownBodyHeight = -1;

    /// <summary>The user's switches (hints, which-key); the defaults until the host loads the file.</summary>
    public AppSettings Settings { get; init; } = AppSettings.Default;

    /// <summary>The open comment editor (Insert mode, or Confirm while it asks to discard), otherwise null.</summary>
    public CommentEditor? Editor { get; init; }

    /// <summary>The open jump-to-issue prompt (Insert mode), otherwise null.</summary>
    public JumpState? Jump { get; init; }

    /// <summary>The open profile list (Insert mode), otherwise null.</summary>
    public ProfileListState? ProfileList { get; init; }

    /// <summary>The open right-click menu (Insert mode), otherwise null.</summary>
    public ContextMenuState? ContextMenu { get; init; }

    /// <summary>The count typed before a key (<c>3</c> in <c>3 Alt-j</c>), 0 when none.</summary>
    public int Count { get; init; }

    public static AppState Initial { get; } =
        new(
            InputMode.Normal,
            string.Empty,
            string.Empty,
            0,
            null,
            null,
            ProfileCatalog.Empty,
            null,
            false,
            SiteSession.None,
            0,
            0);
}
