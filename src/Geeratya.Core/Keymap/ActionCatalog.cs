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
        new(ActionIds.FormNext, "Accept the field and go to the next"),
        new(ActionIds.FormBackspace, "Delete the last character"),
        new(ActionIds.ItemOpen, "Open the selected item"),
        new(ActionIds.NavigateFirstColumn, "Go to the first column"),
        new(ActionIds.NavigateLastColumn, "Go to the last column"),
        new(ActionIds.SectionNext, "Next section of the issue"),
        new(ActionIds.SectionPrevious, "Previous section of the issue"),
        new(ActionIds.TabNext, "Next tab"),
        new(ActionIds.TabPrevious, "Previous tab"),
        new(ActionIds.TabGoTo1, "Go to tab 1"),
        new(ActionIds.TabGoTo2, "Go to tab 2"),
        new(ActionIds.TabGoTo3, "Go to tab 3"),
        new(ActionIds.TabGoTo4, "Go to tab 4"),
        new(ActionIds.TabGoTo5, "Go to tab 5"),
        new(ActionIds.TabGoTo6, "Go to tab 6"),
        new(ActionIds.TabGoTo7, "Go to tab 7"),
        new(ActionIds.TabGoTo8, "Go to tab 8"),
        new(ActionIds.TabGoTo9, "Go to tab 9"),
        new(ActionIds.TabNew, "Open the source picker for a new tab"),
        new(ActionIds.TabClose, "Close the tab"),
        new(ActionIds.ViewRefresh, "Refresh the view (also retries after an error)"),
        new(ActionIds.ViewLayoutToggle, "Switch between list and columns"),
        new(ActionIds.IssueOpenInBrowser, "Open the issue in the browser"),
        new(ActionIds.IssueOpenParent, "Open the parent issue"),
        new(ActionIds.PickerPrevious, "Previous source in the picker"),
        new(ActionIds.PickerNext, "Next source in the picker"),
    ];

    /// <summary>
    /// Actions bound ahead of their feature: the keys resolve but nothing happens yet (no detail screen yet).
    /// </summary>
    public static IReadOnlyCollection<string> Unimplemented { get; } =
    [
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
        ActionIds.FormNext,
        ActionIds.FormBackspace,
        ActionIds.PickerPrevious,
        ActionIds.PickerNext,
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
