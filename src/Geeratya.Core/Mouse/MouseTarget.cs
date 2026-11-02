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
}
