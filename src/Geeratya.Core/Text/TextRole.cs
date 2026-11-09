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

    /// <summary>Struck-through text (an ADF <c>strike</c> mark): grey with a line through it.</summary>
    Strike,

    /// <summary>Text coloured red in a Jira description or comment (an ADF <c>textColor</c> mark); the Tui picks the theme's red.</summary>
    ColorRed,

    /// <summary>Coloured text, yellow or orange.</summary>
    ColorYellow,

    /// <summary>Coloured text, green.</summary>
    ColorGreen,

    /// <summary>Coloured text, teal or cyan.</summary>
    ColorTeal,

    /// <summary>Coloured text, blue.</summary>
    ColorBlue,

    /// <summary>Coloured text, purple or magenta.</summary>
    ColorPurple,

    /// <summary>Coloured text, grey.</summary>
    ColorGray,

    Link,
    Quote,
    Selected,
    Key,

    /// <summary>A key listed in an overlay (which-key, help): bold in the warn colour, as in Gitunia.</summary>
    OverlayKey,

    StatusToDo,
    StatusInProgress,
    StatusDone,
    TypeEpic,
    TypeStory,
    TypeBug,
    TypeTask,
    TypeSubtask,

    /// <summary>The priority glyph of a high, highest, critical or blocker issue.</summary>
    PriorityHigh,

    /// <summary>The priority glyph of a medium issue.</summary>
    PriorityMedium,

    /// <summary>The priority glyph of a low, lowest or minor issue.</summary>
    PriorityLow,
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

    /// <summary>A button on the bar (<c>+</c>, <c>: commands</c>, <c>&lt;space&gt;g jump</c>): bold, in the accent colour.</summary>
    BarButton,

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

    /// <summary>An inactive tab chip: a button apart from the bar.</summary>
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

    /// <summary>The quiet border of a pane that has no focus (<c>docs/internal/ux-look.md</c>, section 4).</summary>
    Border,

    /// <summary>The whole box of the focused pane: accent, bold.</summary>
    FrameFocus,

    /// <summary>The title chip of the focused pane: filled accent, bold.</summary>
    ChipFocus,

    /// <summary>A row in the Visual range: a stronger tint than the selected row (reversed without a tint).</summary>
    Range,

    /// <summary>The dim chip at the right of an overlay's top edge (the palette's <c>12/87</c>): quiet, never the accent.</summary>
    OverlayCount,

    /// <summary>A filled label chip, accent colour of the theme (the first of <see cref="LabelChips"/>).</summary>
    LabelChip0,

    /// <summary>A filled label chip, good colour.</summary>
    LabelChip1,

    /// <summary>A filled label chip, remote colour.</summary>
    LabelChip2,

    /// <summary>A filled label chip, warn colour.</summary>
    LabelChip3,

    /// <summary>A filled label chip, info colour.</summary>
    LabelChip4,

    /// <summary>The rounded cap of a <see cref="LabelChip0"/> chip: the chip's fill colour as text on the page (drawn by the Tui chip caps pass).</summary>
    LabelCap0,

    /// <summary>The cap of a <see cref="LabelChip1"/> chip.</summary>
    LabelCap1,

    /// <summary>The cap of a <see cref="LabelChip2"/> chip.</summary>
    LabelCap2,

    /// <summary>The cap of a <see cref="LabelChip3"/> chip.</summary>
    LabelCap3,

    /// <summary>The cap of a <see cref="LabelChip4"/> chip.</summary>
    LabelCap4,

    /// <summary>The initials chip of an assignee: muted fill.</summary>
    InitialsChip,

    /// <summary>The <c>--</c> chip of an unassigned issue: muted outline, no fill.</summary>
    UnassignedChip,

    /// <summary>A filled value block (a Details value, a comment header bar): a tint one step off the page.</summary>
    ValueBlock,

    /// <summary>The accent bar at the left of a selected row or value: a left-half-block glyph in the accent on the page colour, beside the selection tint.</summary>
    SelectionBar,

    /// <summary>Text in an input being edited in place: the field fill, so the edit state reads as a text box.</summary>
    EditField,

    /// <summary>The caret of an input being edited in place: a filled block (reverse video without colour).</summary>
    EditCaret,
}
