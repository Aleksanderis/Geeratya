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
}
