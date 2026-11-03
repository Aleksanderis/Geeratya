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

    /// <summary>The network banner row under the tab bar.</summary>
    Banner,

    /// <summary>The body while a list or columns view shows (issue rows, columns).</summary>
    Lists,

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

    /// <summary>Everything outside an open overlay or prompt.</summary>
    Backdrop,

    /// <summary>A row of the context menu that a right-click opens (the other actions of the row).</summary>
    ContextMenu,

    /// <summary>A choice row of the source picker overlay.</summary>
    PickerRow,

    /// <summary>A completion row of the jump prompt.</summary>
    JumpRow,

    /// <summary>A profile row of the profile list overlay.</summary>
    ProfileRow,

    /// <summary>A field of an open field form (a click focuses it).</summary>
    FormField,

    /// <summary>A row of the issue type list or of a form field's suggestion popup (a click takes it).</summary>
    FormChoice,

    /// <summary>A row of the comment editor's suggestion popup (a click takes it).</summary>
    EditorChoice,

    /// <summary>The <c>y</c> row of the Confirm dialog: runs the action.</summary>
    ConfirmYes,

    /// <summary>The <c>n</c> row of the Confirm dialog: cancels.</summary>
    ConfirmNo,

    /// <summary>The rest of the Confirm dialog's box: a click there does nothing (outside it cancels).</summary>
    ConfirmBox,
}
