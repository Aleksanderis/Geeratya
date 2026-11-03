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

    /// <summary>An overlay's frame in the accent colour.</summary>
    OverlayFrame,

    /// <summary>The frame of a confirm for an action that changes something (warn colour).</summary>
    OverlayFrameWarn,

    /// <summary>The frame of a confirm for a destructive action (bad colour).</summary>
    OverlayFrameBad,

    /// <summary>An overlay's title: the filled accent chip.</summary>
    OverlayChip,

    /// <summary>The title chip of a warn confirm.</summary>
    OverlayChipWarn,

    /// <summary>The title chip of a destructive confirm.</summary>
    OverlayChipBad,

    /// <summary>The quiet border of a pane that has no focus (<c>docs/ux-look.md</c>, section 4).</summary>
    Border,

    /// <summary>The whole box of the focused pane: accent, bold.</summary>
    FrameFocus,

    /// <summary>The title chip of the focused pane: filled accent, bold.</summary>
    ChipFocus,

    /// <summary>The selected card of a pane without focus: a quieter tint (underlined without a tint).</summary>
    SelectedDim,

    /// <summary>A row in the Visual range: a stronger tint than the selected row (reversed without a tint).</summary>
    Range,

    /// <summary>The dim chip at the right of an overlay's top edge (the palette's <c>12/87</c>): quiet, never the accent.</summary>
    OverlayCount,
}
