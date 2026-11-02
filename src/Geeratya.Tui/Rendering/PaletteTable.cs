using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The role tables: which palette role draws a text role, and the contrast every theme owes a role.</summary>
public static class PaletteTable
{
    private const double TextContrast = 4.5;
    private const double DividerContrast = 3.0;

    private static readonly Dictionary<TextRole, PaletteRole> _textRoles = new()
    {
        [TextRole.Body] = PaletteRole.Body,
        [TextRole.Muted] = PaletteRole.Muted,
        [TextRole.Heading] = PaletteRole.Heading,
        [TextRole.Strong] = PaletteRole.Strong,
        [TextRole.Emphasis] = PaletteRole.Emphasis,
        [TextRole.Code] = PaletteRole.Code,
        [TextRole.Link] = PaletteRole.Link,
        [TextRole.Quote] = PaletteRole.Quote,
        [TextRole.Selected] = PaletteRole.Selected,
        [TextRole.Key] = PaletteRole.Key,
        [TextRole.StatusToDo] = PaletteRole.StatusToDo,
        [TextRole.StatusInProgress] = PaletteRole.StatusInProgress,
        [TextRole.StatusDone] = PaletteRole.StatusDone,
        [TextRole.TypeEpic] = PaletteRole.TypeEpic,
        [TextRole.TypeStory] = PaletteRole.TypeStory,
        [TextRole.TypeBug] = PaletteRole.TypeBug,
        [TextRole.TypeTask] = PaletteRole.TypeTask,
        [TextRole.TypeSubtask] = PaletteRole.TypeSubtask,
        [TextRole.Error] = PaletteRole.Error,
        [TextRole.Loading] = PaletteRole.Loading,
        [TextRole.Offline] = PaletteRole.Offline,
        [TextRole.RateLimited] = PaletteRole.RateLimited,
        [TextRole.Stale] = PaletteRole.Stale,
        [TextRole.FieldLabel] = PaletteRole.FieldLabel,
        [TextRole.Editable] = PaletteRole.Editable,
    };

    public static IReadOnlyList<PaletteRole> Roles { get; } = Enum.GetValues<PaletteRole>();

    /// <summary>The palette role that draws a renderer-agnostic text role.</summary>
    public static PaletteRole For(TextRole role)
    {
        return _textRoles[role];
    }

    /// <summary>The contrast ratio a role must reach against its background: 3.0 for dividers, 4.5 for text.</summary>
    public static double RequiredContrast(PaletteRole role)
    {
        return role == PaletteRole.Divider ? DividerContrast : TextContrast;
    }
}
