namespace Geeratya.Tui.Rendering;

/// <summary>What a piece of text means; views ask the palette for a role, never for a colour.</summary>
public enum PaletteRole
{
    Body,
    Status,
    Muted,
    Divider,
    Error,
    Loading,
    Offline,
    RateLimited,
    Warning,
    Stale,
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
    FieldLabel,
    Editable,
}
