using System.Collections.ObjectModel;
using Geeratya.Core.Keymap;

namespace Geeratya.Core.Mouse;

/// <summary>
/// The mouse table (<c>docs/ux-mouse.md</c>): a gesture on an area becomes the action id a key would produce. A test
/// compares this list with the table in the document and checks that every action a key can run has a path here or
/// an entry in <see cref="Exclusions"/>.
/// </summary>
public static class DefaultMouseMap
{
    /// <summary>Rows moved by one wheel notch.</summary>
    public const int WheelRows = 3;

    public static IReadOnlyList<MouseRow> Rows { get; } =
    [
        new(MouseTarget.Lists, MouseGesture.WheelUp, ActionIds.NavigateUp, WheelRows, 'A'),
        new(MouseTarget.Lists, MouseGesture.WheelDown, ActionIds.NavigateDown, WheelRows, 'A'),
        new(MouseTarget.Detail, MouseGesture.WheelUp, ActionIds.NavigateUp, WheelRows, 'A'),
        new(MouseTarget.Detail, MouseGesture.WheelDown, ActionIds.NavigateDown, WheelRows, 'A'),
        new(MouseTarget.Status, MouseGesture.Click, ActionIds.PaletteOpen, 1, 'A'),
        new(MouseTarget.StatusMessage, MouseGesture.Click, ActionIds.StatusHistory, 1, 'A'),
        new(MouseTarget.Backdrop, MouseGesture.Click, ActionIds.Cancel, 1, 'A'),
        new(MouseTarget.PickerRow, MouseGesture.Click, ActionIds.FormNext, 1, 'O'),
        new(MouseTarget.JumpRow, MouseGesture.Click, ActionIds.FormNext, 1, 'O'),
        new(MouseTarget.PaletteRow, MouseGesture.Click, ActionIds.FormNext, 1, 'O'),
        new(MouseTarget.PaletteRow, MouseGesture.WheelUp, ActionIds.PickerPrevious, WheelRows, 'O'),
        new(MouseTarget.PaletteRow, MouseGesture.WheelDown, ActionIds.PickerNext, WheelRows, 'O'),
        new(MouseTarget.ProfileRow, MouseGesture.Click, ActionIds.FormNext, 1, 'O'),
        new(MouseTarget.ProfileRow, MouseGesture.RightClick, ActionIds.ProfileRemove, 1, 'O'),
        new(MouseTarget.FormField, MouseGesture.Click, ActionIds.FormFieldNext, 1, 'O'),
        new(MouseTarget.FormChoice, MouseGesture.Click, ActionIds.FormNext, 1, 'O'),
        new(MouseTarget.EditorChoice, MouseGesture.Click, ActionIds.FormNext, 1, 'O'),
        new(MouseTarget.HelpPage, MouseGesture.Click, ActionIds.HelpPageNext, 1, 'O'),
        new(MouseTarget.ConfirmYes, MouseGesture.Click, ActionIds.ConfirmYes, 1, 'O'),
        new(MouseTarget.ConfirmNo, MouseGesture.Click, ActionIds.ConfirmNo, 1, 'O'),
        new(MouseTarget.Lists, MouseGesture.DoubleClick, ActionIds.ItemOpen, 1, 'B'),
        new(MouseTarget.Detail, MouseGesture.DoubleClick, ActionIds.ItemOpen, 1, 'B'),
        new(MouseTarget.Lists, MouseGesture.RightClick, ActionIds.ContextMenuOpen, 1, 'M'),
        new(MouseTarget.Detail, MouseGesture.RightClick, ActionIds.ContextMenuOpen, 1, 'M'),
        new(MouseTarget.Tabs, MouseGesture.Click, ActionIds.TabNext, 1, 'B'),
        new(MouseTarget.Tabs, MouseGesture.RightClick, ActionIds.ViewMenuOpen, 1, 'V'),
        new(MouseTarget.Status, MouseGesture.RightClick, ActionIds.ViewMenuOpen, 1, 'V'),
        new(MouseTarget.StatusMessage, MouseGesture.RightClick, ActionIds.ViewMenuOpen, 1, 'V'),
        new(MouseTarget.NewTabButton, MouseGesture.Click, ActionIds.TabNew, 1, 'B'),
        new(MouseTarget.JumpChip, MouseGesture.Click, ActionIds.IssueJump, 1, 'B'),
        new(MouseTarget.Banner, MouseGesture.Click, ActionIds.ViewRefresh, 1, 'B'),
        new(MouseTarget.StatusBadge, MouseGesture.Click, ActionIds.IssueTransition, 1, 'C'),
        new(MouseTarget.Assignee, MouseGesture.Click, ActionIds.IssueAssign, 1, 'C'),
        new(MouseTarget.NewIssueButton, MouseGesture.Click, ActionIds.IssueCreate, 1, 'C'),
        new(MouseTarget.CommentsAdd, MouseGesture.Click, ActionIds.CommentAdd, 1, 'C'),
        new(MouseTarget.Comment, MouseGesture.Click, ActionIds.CommentEdit, 1, 'C'),
        new(MouseTarget.Lists, MouseGesture.Drag, ActionIds.IssueTransitionNext, 1, 'M'),
        new(MouseTarget.Lists, MouseGesture.CtrlClick, ActionIds.SelectionMark, 0, 'B'),
        new(MouseTarget.Lists, MouseGesture.DragRows, ActionIds.SelectionVisual, 1, 'B'),
        new(MouseTarget.Lists, MouseGesture.DragToSprint, ActionIds.SprintMoveIssues, 1, 'M'),
        new(MouseTarget.Lists, MouseGesture.DragToBacklog, ActionIds.SprintMoveToBacklog, 1, 'M'),
        ContextMenuRow(ActionIds.ViewLayoutToggle, 'V'),
        ContextMenuRow(ActionIds.IssueOpenInBrowser),
        ContextMenuRow(ActionIds.IssueOpenParent),
        ContextMenuRow(ActionIds.IssueEdit),
        ContextMenuRow(ActionIds.IssueAssignMe),
        ContextMenuRow(ActionIds.IssueTransitionPrevious),
        ContextMenuRow(ActionIds.IssueDelete),
        ContextMenuRow(ActionIds.IssueCreateSubtask),
        ContextMenuRow(ActionIds.IssueLink),
        ContextMenuRow(ActionIds.IssueUnlink),
        ContextMenuRow(ActionIds.IssueJump, 'V'),
        ContextMenuRow(ActionIds.SprintSwitch, 'V'),
        ContextMenuRow(ActionIds.SprintCreate, 'V'),
        ContextMenuRow(ActionIds.SprintEdit, 'V'),
        ContextMenuRow(ActionIds.SprintStart, 'V'),
        ContextMenuRow(ActionIds.SprintComplete, 'V'),
        ContextMenuRow(ActionIds.SprintGoal, 'V'),
        ContextMenuRow(ActionIds.StatusHistory, 'V'),
        ContextMenuRow(ActionIds.StatusDismiss, 'V'),
        ContextMenuRow(ActionIds.RankUp),
        ContextMenuRow(ActionIds.RankDown),
        ContextMenuRow(ActionIds.RankTop),
        ContextMenuRow(ActionIds.RankBottom),
        ContextMenuRow(ActionIds.SectionToggle),
        ContextMenuRow(ActionIds.RankTo),
        ContextMenuRow(ActionIds.SprintMoveIssues),
        ContextMenuRow(ActionIds.SprintMoveToBacklog),
        ContextMenuRow(ActionIds.CommentDelete),
        ContextMenuRow(ActionIds.TabClose, 'V'),
        ContextMenuRow(ActionIds.TabPrevious, 'V'),
        ContextMenuRow(ActionIds.HelpToggle, 'V'),
        ContextMenuRow(ActionIds.Quit, 'V'),
    ];

    /// <summary>
    /// Actions that deliberately have no mouse path of their own, with the reason: motions that the pointer does by
    /// pointing, and numbered tab jumps that a click on the tab does.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Exclusions { get; } = BuildExclusions();

    public static MouseRow? Find(MouseTarget target, MouseGesture gesture)
    {
        foreach (var row in Rows)
        {
            if (row.Target == target && row.Gesture == gesture)
            {
                return row;
            }
        }

        return null;
    }

    private static MouseRow ContextMenuRow(string action, char part = 'M')
    {
        return new MouseRow(MouseTarget.ContextMenu, MouseGesture.Click, action, 1, part);
    }

    private static ReadOnlyDictionary<string, string> BuildExclusions()
    {
        const string pointing = "motion: the pointer places the cursor and the wheel scrolls";
        const string helpScroll = "scrolls the help page; the wheel does nothing while help is open (the section names are clickable)";
        const string tabClick = "a click on tab N (Tabs row)";

        var exclusions = new Dictionary<string, string>
        {
            [ActionIds.NavigateDown] = "wheel rows cover it",
            [ActionIds.NavigateUp] = "wheel rows cover it",
            [ActionIds.NavigateLeft] = pointing,
            [ActionIds.NavigateRight] = pointing,
            [ActionIds.NavigateTop] = pointing,
            [ActionIds.NavigateBottom] = pointing,
            [ActionIds.NavigatePageDown] = pointing,
            [ActionIds.NavigatePageUp] = pointing,
            [ActionIds.NavigateFirstColumn] = pointing,
            [ActionIds.NavigateLastColumn] = pointing,
            [ActionIds.SectionNext] = "a click on a section title of the issue places the cursor there",
            [ActionIds.SectionPrevious] = "a click on a section title of the issue places the cursor there",
            [ActionIds.TabLastActive] = "a click on the tab bar picks any tab directly; the menu of the tab and the view has no row for it",
            [ActionIds.TabGoTo1] = tabClick,
            [ActionIds.TabGoTo2] = tabClick,
            [ActionIds.TabGoTo3] = tabClick,
            [ActionIds.TabGoTo4] = tabClick,
            [ActionIds.TabGoTo5] = tabClick,
            [ActionIds.TabGoTo6] = tabClick,
            [ActionIds.TabGoTo7] = tabClick,
            [ActionIds.TabGoTo8] = tabClick,
            [ActionIds.TabGoTo9] = tabClick,
            [ActionIds.HelpPagePrevious] = "a click on the section name shows that section",
            [ActionIds.HelpScrollDown] = helpScroll,
            [ActionIds.HelpScrollUp] = helpScroll,
            [ActionIds.HelpHalfPageDown] = helpScroll,
            [ActionIds.HelpHalfPageUp] = helpScroll,
            [ActionIds.HelpFilter] = "the filter is typed text; the section names are the mouse path",
            [ActionIds.PickerOpenNewTab] = "the sprint picker only: a click on a row opens it like Enter, the key adds the new tab",
            [ActionIds.ViewCycleCommandLevel] = "its palette row (click the status line, then the row) runs it",
        };

        return new ReadOnlyDictionary<string, string>(exclusions);
    }
}
