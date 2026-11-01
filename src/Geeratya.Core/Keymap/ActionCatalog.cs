namespace Geeratya.Core.Keymap;

/// <summary>Every action a key can run, with its title.</summary>
public static class ActionCatalog
{
    public static IReadOnlyList<ActionDefinition> All { get; } =
    [
        new(ActionIds.NavigateDown, "Move down"),
        new(ActionIds.NavigateUp, "Move up"),
        new(ActionIds.NavigateLeft, "Move left"),
        new(ActionIds.NavigateRight, "Move right"),
        new(ActionIds.NavigateTop, "Go to the top"),
        new(ActionIds.NavigateBottom, "Go to the bottom"),
        new(ActionIds.NavigatePageDown, "Page down"),
        new(ActionIds.NavigatePageUp, "Page up"),
        new(ActionIds.HelpToggle, "Show or hide help"),
        new(ActionIds.HelpClose, "Close help"),
        new(ActionIds.Quit, "Quit"),
        new(ActionIds.Cancel, "Cancel or close"),
        new(ActionIds.PaletteOpen, "Open the command palette"),
        new(ActionIds.PaletteSubmit, "Run the typed command"),
        new(ActionIds.PaletteBackspace, "Delete the last character"),
        new(ActionIds.ConfirmYes, "Confirm"),
        new(ActionIds.ConfirmNo, "Decline"),
    ];

    /// <summary>
    /// Actions bound ahead of their feature: the keys resolve but nothing happens yet (the shell has no list).
    /// </summary>
    public static IReadOnlyCollection<string> Unimplemented { get; } =
    [
        ActionIds.NavigateDown,
        ActionIds.NavigateUp,
        ActionIds.NavigateLeft,
        ActionIds.NavigateRight,
        ActionIds.NavigateTop,
        ActionIds.NavigateBottom,
        ActionIds.NavigatePageDown,
        ActionIds.NavigatePageUp,
    ];

    /// <summary>
    /// Actions that exist only inside a modal mode (help, command, confirm); they are never bound in Normal mode.
    /// </summary>
    public static IReadOnlyCollection<string> ModalOnly { get; } =
    [
        ActionIds.HelpClose,
        ActionIds.PaletteSubmit,
        ActionIds.PaletteBackspace,
        ActionIds.ConfirmYes,
        ActionIds.ConfirmNo,
    ];

    public static bool IsModalOnly(string id)
    {
        return ModalOnly.Contains(id);
    }

    public static string? FindTitle(string id)
    {
        foreach (var definition in All)
        {
            if (definition.Id == id)
            {
                return definition.Title;
            }
        }

        return null;
    }
}
