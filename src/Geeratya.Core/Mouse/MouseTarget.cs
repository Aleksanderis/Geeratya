namespace Geeratya.Core.Mouse;

/// <summary>
/// The screen areas a gesture is resolved against; the mouse table in <c>docs/ux-mouse.md</c> is keyed by these.
/// </summary>
public enum MouseTarget
{
    /// <summary>A tab in the tab bar row.</summary>
    Tabs,

    /// <summary>The <c>+</c> button after the last tab.</summary>
    NewTabButton,

    /// <summary>The <c>x</c> on the active tab chip (closes that tab, same as <c>tab.close</c>).</summary>
    TabClose,

    /// <summary>The <c> : commands </c> chip at the right end of the tab bar row (opens the command palette).</summary>
    CommandsChip,

    /// <summary>The jump chip at the right end of the tab bar row (opens the jump prompt).</summary>
    JumpChip,

    /// <summary>The <c>Lane n/N: Name</c> text of a board with swimlanes in the status line (a click steps to the next lane).</summary>
    LaneChip,

    /// <summary>The network banner row under the tab bar.</summary>
    Banner,

    /// <summary>The body while a list or columns view shows (issue rows, columns).</summary>
    Lists,

    /// <summary>
    /// The sub-task row of a card in the columns layout (<c>▸ Sub-tasks 1/3</c>): a click expands or collapses its
    /// list. It lies inside <see cref="Lists"/>: the hit tester does not report it: the reducer finds it from the card
    /// geometry after the click placed the cursor and runs this table row's action, so the row is the contract.
    /// </summary>
    SubtaskRow,

    /// <summary>The body while an issue is opened (sections, comments, links).</summary>
    Detail,

    /// <summary>The status badge of the selected or opened issue.</summary>
    StatusBadge,

    /// <summary>The assignee of the selected or opened issue.</summary>
    Assignee,

    /// <summary>The <c>+</c> button in the view header.</summary>
    NewIssueButton,

    /// <summary>The <c>+</c> in the comments section of an opened issue.</summary>
    CommentsAdd,

    /// <summary>A comment of an opened issue.</summary>
    Comment,

    /// <summary>The status line.</summary>
    Status,

    /// <summary>The text of the status message inside the status line.</summary>
    StatusMessage,

    /// <summary>Everything outside an open overlay or prompt.</summary>
    Backdrop,

    /// <summary>A row of the context menu that a right-click opens (the other actions of the row).</summary>
    ContextMenu,

    /// <summary>The Help overlay for the wheel: it scrolls wherever the pointer is, over the popup or not.</summary>
    HelpBody,

    /// <summary>The Messages overlay for the wheel (same: anywhere on the screen).</summary>
    MessagesBody,

    /// <summary>A choice row of the source picker overlay.</summary>
    PickerRow,

    /// <summary>A completion row of the jump prompt.</summary>
    JumpRow,

    /// <summary>An action row of the command palette (a click runs it, the wheel moves the highlight).</summary>
    PaletteRow,

    /// <summary>A category of the settings screen (a click moves the cursor to its first row).</summary>
    SettingsCategory,

    /// <summary>A row of the settings screen (a click acts like Enter, a right-click resets, the wheel moves).</summary>
    SettingsRow,

    /// <summary>A word of the open choice list of the settings screen (a click takes it).</summary>
    SettingsChoice,

    /// <summary>A profile row of the profile manager screen.</summary>
    ProfileRow,

    /// <summary>The Add button of the profile manager screen.</summary>
    ProfileAddButton,

    /// <summary>The Edit button of the profile manager screen (edits the highlighted profile).</summary>
    ProfileEditButton,

    /// <summary>The Test connection button of the profile form.</summary>
    ProfileTestButton,

    /// <summary>The Save button of the profile form.</summary>
    ProfileSaveButton,

    /// <summary>The Cancel button of the profile form.</summary>
    ProfileCancelButton,

    /// <summary>A field of an open field form (a click focuses it).</summary>
    FormField,

    /// <summary>A row of the issue type list or of a form field's suggestion popup (a click takes it).</summary>
    FormChoice,

    /// <summary>A row of the comment editor's suggestion popup (a click takes it).</summary>
    EditorChoice,

    /// <summary>A page name in the first rows of the help overlay (a click shows that page).</summary>
    HelpPage,

    /// <summary>The <c>y</c> row of the Confirm dialog: runs the action.</summary>
    ConfirmYes,

    /// <summary>The <c>n</c> row of the Confirm dialog: cancels.</summary>
    ConfirmNo,

    /// <summary>The rest of the Confirm dialog's box: a click there does nothing (outside it cancels).</summary>
    ConfirmBox,

    /// <summary>
    /// The rest of any other overlay's box: a click there does nothing (outside it is the backdrop and closes it).
    /// </summary>
    OverlayBox,
}
