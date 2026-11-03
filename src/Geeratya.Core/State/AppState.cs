using Geeratya.Core.Comments;
using Geeratya.Core.Jump;
using Geeratya.Core.Menus;
using Geeratya.Core.Keymap;
using Geeratya.Core.Profiles;
using Geeratya.Core.Settings;
using Geeratya.Core.Sources;
using Geeratya.Core.Text;

namespace Geeratya.Core.State;

/// <summary>Everything the shell shows; a new value comes out of every command and nothing mutates it.</summary>
/// <param name="Mode">The current input mode.</param>
/// <param name="PendingKeys">Keys typed so far of a multi-key binding such as <c>g g</c>, empty otherwise.</param>
/// <param name="PaletteText">The text typed after <c>:</c> in Command mode.</param>
/// <param name="HelpScroll">The first visible line of the help overlay.</param>
/// <param name="Message">The text of the notice the status line shows, or null; writing a new text posts it (<see cref="StatusMessages"/>).</param>
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

    /// <summary>
    /// The theme names the host offers (the palette lists one row for each); Core knows no themes, so the host sets
    /// them at start. Empty until then.
    /// </summary>
    public IReadOnlyList<string> ThemeNames { get; init; } = [];

    /// <summary>The open comment editor (Insert mode, or Confirm while it asks to discard), otherwise null.</summary>
    public CommentEditor? Editor { get; init; }

    /// <summary>The open jump-to-issue prompt (Insert mode), otherwise null.</summary>
    public JumpState? Jump { get; init; }

    /// <summary>
    /// The open profile manager, the full-body screen (Insert mode; it stays open under the profile form and the
    /// remove question), otherwise null.
    /// </summary>
    public ProfileListState? ProfileList { get; init; }

    /// <summary>
    /// Where each profile's token comes from, by <see cref="Profile.CredentialKey"/>; filled by the host when the
    /// profile manager opens or the profiles change. A profile missing here is still being looked up.
    /// </summary>
    public IReadOnlyDictionary<string, TokenSourceKind> TokenSources { get; init; } = new Dictionary<string, TokenSourceKind>();

    /// <summary>The open right-click menu (Insert mode), otherwise null.</summary>
    public ContextMenuState? ContextMenu { get; init; }

    /// <summary>
    /// The notice the status line shows, with its severity and lifetime; null when none. <see cref="Message"/> mirrors its
    /// text (see <see cref="StatusMessages"/>).
    /// </summary>
    public StatusMessage? Status { get; init; }

    /// <summary>
    /// The severity the next text written to <see cref="Message"/> is posted with; reset once it is posted.
    /// </summary>
    public StatusSeverity MessageSeverity { get; init; }

    /// <summary>The last 50 messages, newest first.</summary>
    public IReadOnlyList<StatusMessage> StatusHistory { get; init; } = [];

    /// <summary>The problems found while starting, in the order found; the status line shows the first with a count.</summary>
    public IReadOnlyList<string> StartupProblems { get; init; } = [];

    /// <summary>The help overlay shows the message history instead of the key reference.</summary>
    public bool MessagesOpen { get; init; }

    /// <summary>
    /// The Messages overlay lists every setting (value, default, help) above the history (<c>:set</c> without an
    /// argument).
    /// </summary>
    public bool MessagesShowSettings { get; init; }

    /// <summary>
    /// The page of the help overlay (an index into <c>HelpPages.Names</c>); 0 is the active layout's page.
    /// </summary>
    public int HelpPage { get; init; }

    /// <summary>The text of the help filter (<c>/</c>); empty when none.</summary>
    public string HelpFilter { get; init; } = string.Empty;

    /// <summary>Units between the caret and the end of <see cref="PaletteText"/> (<see cref="PromptText"/>).</summary>
    public int PaletteBack { get; init; }

    /// <summary>Units between the caret and the end of <see cref="HelpFilter"/>.</summary>
    public int HelpFilterBack { get; init; }

    /// <summary>
    /// The help filter line is being typed: printable keys are text and <c>Enter</c> keeps the filter.
    /// </summary>
    public bool HelpFiltering { get; init; }

    /// <summary>The highlighted row of the command palette, counted over its selectable rows (Command mode).</summary>
    public int PaletteSelected { get; init; }

    /// <summary>The count typed before a key (<c>3</c> in <c>3 Alt-j</c>), 0 when none.</summary>
    public int Count { get; init; }

    /// <summary>
    /// When the first key of the pending multi-key binding was typed; the which-key delay counts from it.
    /// </summary>
    public DateTimeOffset? PendingSince { get; init; }

    /// <summary>
    /// The <see cref="PendingSince"/> of the chord whose which-key popup is open (set by a tick once the delay has
    /// passed); the popup shows while it equals <see cref="PendingSince"/>, so a new chord starts closed.
    /// </summary>
    public DateTimeOffset? WhichKeyOpenFor { get; init; }

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
