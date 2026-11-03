namespace Geeratya.Core.Keymap;

/// <summary>Every action a key can run, with its title.</summary>
public static class ActionCatalog
{
    private static readonly Dictionary<string, ActionScope> _scopes = new()
    {
        [ActionIds.NavigateDown] = ActionScope.Views,
        [ActionIds.NavigateUp] = ActionScope.Views,
        [ActionIds.NavigateTop] = ActionScope.Views,
        [ActionIds.NavigateBottom] = ActionScope.Views,
        [ActionIds.NavigatePageDown] = ActionScope.Views,
        [ActionIds.NavigatePageUp] = ActionScope.Views,
        [ActionIds.NavigateLeft] = ActionScope.Columns,
        [ActionIds.NavigateRight] = ActionScope.Columns,
        [ActionIds.NavigateFirstColumn] = ActionScope.Columns,
        [ActionIds.NavigateLastColumn] = ActionScope.Columns,
        [ActionIds.ItemOpen] = ActionScope.Views,
        [ActionIds.SectionNext] = ActionScope.Detail | ActionScope.Backlog | ActionScope.Columns,
        [ActionIds.SectionPrevious] = ActionScope.Detail | ActionScope.Backlog | ActionScope.Columns,
        [ActionIds.SectionToggle] = ActionScope.Backlog,
        [ActionIds.SprintCreate] = ActionScope.Backlog | ActionScope.List,
        [ActionIds.SprintEdit] = ActionScope.Backlog | ActionScope.List,
        [ActionIds.SprintStart] = ActionScope.Backlog | ActionScope.List,
        [ActionIds.SprintComplete] = ActionScope.Backlog | ActionScope.List,
        [ActionIds.ContextMenuOpen] = ActionScope.Views,
        [ActionIds.ViewLayoutToggle] = ActionScope.Lists,
        [ActionIds.ViewQuickFilter] = ActionScope.Columns | ActionScope.Backlog,
        [ActionIds.ViewSwimlane] = ActionScope.Columns,
        [ActionIds.IssueOpenInBrowser] = ActionScope.Views,
        [ActionIds.IssueOpenParent] = ActionScope.Detail,
        [ActionIds.IssueEdit] = ActionScope.Views,
        [ActionIds.IssueAssign] = ActionScope.Views,
        [ActionIds.IssueAssignMe] = ActionScope.Views,
        [ActionIds.IssueTransition] = ActionScope.Views,
        [ActionIds.IssueTransitionNext] = ActionScope.Columns,
        [ActionIds.IssueTransitionPrevious] = ActionScope.Columns,
        [ActionIds.RankUp] = ActionScope.Lists,
        [ActionIds.RankDown] = ActionScope.Lists,
        [ActionIds.RankTop] = ActionScope.Lists,
        [ActionIds.RankBottom] = ActionScope.Lists,
        [ActionIds.RankTo] = ActionScope.Lists,
        [ActionIds.SprintMoveIssues] = ActionScope.Lists,
        [ActionIds.SprintMoveToBacklog] = ActionScope.Lists,
        [ActionIds.SelectionVisual] = ActionScope.List | ActionScope.Backlog,
        [ActionIds.SelectionMark] = ActionScope.Lists,
        [ActionIds.IssueDelete] = ActionScope.Views,
        [ActionIds.IssueCreateSubtask] = ActionScope.Views,
        [ActionIds.IssueLink] = ActionScope.Views,
        [ActionIds.IssueUnlink] = ActionScope.Detail,
        [ActionIds.CommentAdd] = ActionScope.Detail,
        [ActionIds.CommentEdit] = ActionScope.Detail,
        [ActionIds.CommentDelete] = ActionScope.Detail,
    };

    /// <summary>The ids the scope table names (for the test that checks them against the catalog).</summary>
    public static IReadOnlyCollection<string> ScopedIds => _scopes.Keys;

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
        new(ActionIds.HelpFilter, "Filter the help by key, title or action id"),
        new(ActionIds.HelpPageNext, "Next help section"),
        new(ActionIds.HelpPagePrevious, "Previous help section"),
        new(ActionIds.HelpScrollDown, "Scroll help down a line"),
        new(ActionIds.HelpScrollUp, "Scroll help up a line"),
        new(ActionIds.HelpHalfPageDown, "Scroll help down half a page"),
        new(ActionIds.HelpHalfPageUp, "Scroll help up half a page"),
        new(ActionIds.Quit, "Quit"),
        new(ActionIds.Cancel, "Cancel or close"),
        new(ActionIds.PaletteOpen, "Open the command palette"),
        new(ActionIds.ContextMenuOpen, "Open the actions menu of the selected issue"),
        new(ActionIds.ViewMenuOpen, "Open the actions menu of the tab and the view"),
        new(ActionIds.PaletteSubmit, "Run the typed command"),
        new(ActionIds.PaletteBackspace, "Delete the last character"),
        new(ActionIds.ConfirmYes, "Confirm"),
        new(ActionIds.ConfirmNo, "Decline"),
        new(ActionIds.FormNext, "Accept the field and go to the next"),
        new(ActionIds.FormBackspace, "Delete the last character"),
        new(ActionIds.ItemOpen, "Open the selected item"),
        new(ActionIds.NavigateFirstColumn, "Go to the first column"),
        new(ActionIds.NavigateLastColumn, "Go to the last column"),
        new(ActionIds.SectionNext, "Next section of the issue or the Scrum backlog"),
        new(ActionIds.SectionPrevious, "Previous section of the issue or the Scrum backlog"),
        new(ActionIds.SectionToggle, "Collapse or expand the sprint section of the Scrum backlog"),
        new(ActionIds.TabNext, "Next tab"),
        new(ActionIds.TabPrevious, "Previous tab"),
        new(ActionIds.TabLastActive, "Previously active tab"),
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
        new(ActionIds.ViewQuickFilter, "Cycle the quick filter of the board (off, then each filter in turn)"),
        new(ActionIds.ViewSwimlane, "Group the board columns into lanes (none, assignee, epic)"),
        new(ActionIds.ViewCycleCommandLevel, "Switch the command level for the session"),
        new(ActionIds.IssueOpenInBrowser, "Open the issue in the browser"),
        new(ActionIds.IssueOpenParent, "Open the parent issue"),
        new(ActionIds.IssueEdit, "Edit fields of the issue (summary, labels, priority, description)"),
        new(ActionIds.IssueAssign, "Assign the issue: search people, or unassign"),
        new(ActionIds.IssueAssignMe, "Assign the issue to me"),
        new(ActionIds.IssueTransition, "Change the status of the issue"),
        new(ActionIds.IssueTransitionNext, "Move the card to the next column"),
        new(ActionIds.IssueTransitionPrevious, "Move the card to the previous column"),
        new(ActionIds.RankUp, "Rank the issue up (a count moves it that many places)"),
        new(ActionIds.RankDown, "Rank the issue down (a count moves it that many places)"),
        new(ActionIds.RankTop, "Rank the issue to the top of the list or column", CommandLevel.Advanced),
        new(ActionIds.RankBottom, "Rank the issue to the bottom of the list or column", CommandLevel.Advanced),
        new(ActionIds.RankTo, "Pick up the issue, then drop it at the issue under the cursor", CommandLevel.Advanced),
        new(ActionIds.SelectionVisual, "Start or end Visual mode (a range of rows)"),
        new(ActionIds.SelectionMark, "Mark or unmark the selected row and move down"),
        new(ActionIds.IssueDelete, "Delete the issue (asks first; Jira cannot undo it)", CommandLevel.Advanced),
        new(ActionIds.IssueCreate, "Create an issue (type picker, then the form)"),
        new(ActionIds.IssueCreateSubtask, "Create a sub-task of the selected issue"),
        new(ActionIds.IssueLink, "Link the issue to another issue", CommandLevel.Advanced),
        new(
            ActionIds.IssueUnlink,
            "Remove the link under the cursor (asks first; Jira cannot undo it)",
            CommandLevel.Advanced),
        new(ActionIds.IssueJump, "Jump to an issue by key"),
        new(ActionIds.StatusHistory, "Show the message history"),
        new(ActionIds.StatusDismiss, "Dismiss the status message"),
        new(ActionIds.ProfileRemove, "Remove the highlighted profile (asks first; the stored token is deleted)"),
        new(ActionIds.PickerPrevious, "Previous row in the picker"),
        new(ActionIds.PickerNext, "Next row in the picker"),
        new(ActionIds.PickerOpenNewTab, "Open the highlighted sprint in a new tab"),
        new(ActionIds.SprintSwitch, "Switch the sprint shown (active, future, closed)"),
        new(ActionIds.SprintMoveIssues, "Move the selected issues to a sprint"),
        new(ActionIds.SprintMoveToBacklog, "Move the selected issues to the backlog"),
        new(ActionIds.SprintCreate, "Create a sprint (name, goal, dates)"),
        new(ActionIds.SprintEdit, "Edit the sprint (name, goal, dates)", CommandLevel.Advanced),
        new(ActionIds.SprintStart, "Start the sprint (confirms dates and issue count)"),
        new(ActionIds.SprintComplete, "Complete the sprint (confirms where the open issues go)"),
        new(ActionIds.SprintGoal, "Show the full goal of the sprint"),
        new(ActionIds.CommentAdd, "Add a comment to the issue"),
        new(ActionIds.CommentEdit, "Edit the comment under the cursor"),
        new(ActionIds.CommentDelete, "Delete the comment under the cursor", CommandLevel.Advanced),
        new(ActionIds.EditorLeft, "Move the cursor left"),
        new(ActionIds.EditorRight, "Move the cursor right"),
        new(ActionIds.EditorRowStart, "Move the cursor to the start of the row"),
        new(ActionIds.EditorRowEnd, "Move the cursor to the end of the row"),
        new(ActionIds.EditorDelete, "Delete the character under the cursor"),
        new(ActionIds.EditorPageUp, "Move the cursor up half a screen"),
        new(ActionIds.EditorPageDown, "Move the cursor down half a screen"),
        new(ActionIds.EditorWordLeft, "Move the cursor to the start of the word"),
        new(ActionIds.EditorWordRight, "Move the cursor to the end of the word"),
        new(ActionIds.FormFieldNext, "Next field of the form"),
        new(ActionIds.FormFieldPrevious, "Previous field of the form"),
        new(ActionIds.FormSubmit, "Save the form"),
        new(ActionIds.FormOpenAfter, "Open the new issue after it is created (on or off)"),
        new(ActionIds.FormOpenBrowser, "Open the issue page in the browser from the form"),
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
        ActionIds.HelpFilter,
        ActionIds.HelpPageNext,
        ActionIds.HelpPagePrevious,
        ActionIds.HelpScrollDown,
        ActionIds.HelpScrollUp,
        ActionIds.HelpHalfPageDown,
        ActionIds.HelpHalfPageUp,
        ActionIds.PaletteSubmit,
        ActionIds.PaletteBackspace,
        ActionIds.ConfirmYes,
        ActionIds.ConfirmNo,
        ActionIds.FormNext,
        ActionIds.FormBackspace,
        ActionIds.PickerPrevious,
        ActionIds.PickerNext,
        ActionIds.PickerOpenNewTab,
        ActionIds.ProfileRemove,
        ActionIds.EditorLeft,
        ActionIds.EditorRight,
        ActionIds.EditorRowStart,
        ActionIds.EditorRowEnd,
        ActionIds.EditorDelete,
        ActionIds.EditorPageUp,
        ActionIds.EditorPageDown,
        ActionIds.EditorWordLeft,
        ActionIds.EditorWordRight,
        ActionIds.FormFieldNext,
        ActionIds.FormFieldPrevious,
        ActionIds.FormSubmit,
        ActionIds.FormOpenAfter,
        ActionIds.FormOpenBrowser,
    ];

    public static bool IsModalOnly(string id)
    {
        return ModalOnly.Contains(id);
    }

    /// <summary>The palette's groups in display order; <see cref="GroupOf"/> names one of them.</summary>
    public static IReadOnlyList<string> Groups { get; } =
    [
        "Issue",
        "Comment",
        "Rank",
        "Selection",
        "View",
        "Tabs",
        "Navigate",
        "General",
    ];

    /// <summary>
    /// The palette group of an action (<c>docs/ux.md</c>, "Command palette"), from the id's prefix so a new action
    /// of a known family needs no entry; null for an action in no group (a test fails when a bound action has none).
    /// </summary>
    public static string? GroupOf(string id)
    {
        if (id == ActionIds.ViewMenuOpen || id == ActionIds.SprintSwitch || id == ActionIds.SprintGoal || id.StartsWith("view.", StringComparison.Ordinal))
        {
            return "View";
        }

        if (id == ActionIds.SprintMoveIssues || id == ActionIds.SprintMoveToBacklog)
        {
            return "Issue";
        }

        if (id == ActionIds.ContextMenuOpen)
        {
            return "General";
        }

        var dot = id.IndexOf('.', StringComparison.Ordinal);

        return (dot < 0 ? id : id[..dot]) switch
        {
            "issue" => "Issue",
            "comment" => "Comment",
            "rank" => "Rank",
            "selection" => "Selection",
            "tab" => "Tabs",
            "sprint" => "View",
            "nav" or "item" or "detail" or "section" => "Navigate",
            "help" or "app" or "palette" or "mode" or "status" => "General",
            _ => null,
        };
    }

    /// <summary>
    /// The layouts an action applies to (<c>docs/ux.md</c>, "Help"): the tag the help overlay sorts its layout pages
    /// by. An action not named in the table applies everywhere (<see cref="ActionScope.Global"/>); a test checks
    /// that the table names only catalog actions.
    /// </summary>
    public static ActionScope ScopeOf(string id)
    {
        return _scopes.GetValueOrDefault(id, ActionScope.Global);
    }

    /// <summary>
    /// The command level of an action; an id the catalog does not know counts as everyday, so a stray id is
    /// never hidden.
    /// </summary>
    public static CommandLevel LevelOf(string id)
    {
        foreach (var definition in All)
        {
            if (definition.Id == id)
            {
                return definition.Level;
            }
        }

        return CommandLevel.Everyday;
    }

    /// <summary>
    /// True when the lists of <paramref name="level"/> show the action (a level hides, it never disables).
    /// </summary>
    public static bool IsShownAt(string id, CommandLevel level)
    {
        return LevelOf(id) <= level;
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
