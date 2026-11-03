namespace Geeratya.Core.Text;

/// <summary>What a run of text means. Renderer-agnostic: the Tui maps each role to a palette role, so Core never names a colour.</summary>
public enum TextRole
{
    Body,
    Muted,
    Heading,
    Strong,
    Emphasis,
    Code,
    Link,
    Quote,
    Selected,
    Key,
    StatusToDo,
    StatusInProgress,
    StatusDone,
    TypeEpic,
    TypeStory,
    TypeBug,
    TypeTask,
    TypeSubtask,
    Error,
    Loading,
    Offline,
    RateLimited,
    Warning,
    Stale,
    FieldLabel,
    Editable,

    /// <summary>Plain text on the bar background (the tab row and the status bar).</summary>
    BarText,

    /// <summary>Dim text on the bar background: a quiet label such as the <c>+</c> button.</summary>
    BarMuted,

    /// <summary>A key of a hint chip on the bar: bold, in the key colour.</summary>
    BarKey,

    /// <summary>An error message on the bar background.</summary>
    BarError,

    /// <summary>A warning message on the bar background.</summary>
    BarWarning,

    /// <summary>An info or success message on the bar background.</summary>
    BarSuccess,

    /// <summary>A fresh info message on the bar: a filled chip.</summary>
    BarSuccessChip,

    /// <summary>The active tab chip: filled accent, bold.</summary>
    TabActive,

    /// <summary>An inactive tab chip: muted text on the bar.</summary>
    TabDim,

    /// <summary>The Normal mode badge at column 0 of the status bar.</summary>
    BadgeNormal,

    /// <summary>The Visual mode badge.</summary>
    BadgeVisual,

    /// <summary>The Insert mode badge.</summary>
    BadgeInsert,

    /// <summary>The Command mode badge.</summary>
    BadgeCommand,

    /// <summary>The Confirm mode badge.</summary>
    BadgeConfirm,

    /// <summary>The Help mode (and Messages) badge.</summary>
    BadgeHelp,
}
