using Geeratya.Core.Backlog;
using Geeratya.Core.Comments;
using Geeratya.Core.Detail;
using Geeratya.Core.Keymap;
using Geeratya.Core.Sprints;
using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Menus;

/// <summary>
/// Builds the right-click menu: only the actions that act on the issue under the pointer (the row of a list, the
/// card, or the line of an opened issue), not the whole command palette. Pure. Every item runs through the same
/// handler as its key, so the destructive ones (<c>issue.delete</c>, <c>comment.delete</c>, <c>issue.unlink</c>)
/// still ask first.
/// </summary>
public static class ContextMenus
{
    private static readonly string[] _issueActions =
    [
        ActionIds.IssueOpenInBrowser,
        ActionIds.IssueOpenParent,
        ActionIds.YankKey,
        ActionIds.YankLink,
        ActionIds.YankSummary,
        ActionIds.IssueEdit,
        ActionIds.IssueAssignMe,
        ActionIds.IssueTransitionPrevious,
        ActionIds.IssueCreateSubtask,
        ActionIds.IssueLink,
        ActionIds.IssueUnlink,
        ActionIds.IssueDelete,
    ];

    private static readonly string[] _rankActions =
    [
        ActionIds.RankUp,
        ActionIds.RankDown,
        ActionIds.RankTop,
        ActionIds.RankBottom,
        ActionIds.RankTo,
    ];

    /// <summary>The menu for the cursor of the active tab, null when there is no issue under it.</summary>
    public static ContextMenuState? For(AppState state)
    {
        var tab = state.Session.Active is { } id
            ? state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == id)
            : null;

        if (tab is null || (tab.FocusedSprintId is not null && tab.Detail.Count == 0))
        {
            return null;
        }

        var actions = new List<string>(_issueActions);
        var parent = tab.Detail.Count > 0
            ? tab.Detail[^1].Issue?.Parent?.Key ?? tab.Detail[^1].Row?.Parent?.Key
            : tab.Rows.ElementAtOrDefault(tab.Selected)?.Parent?.Key;

        if (parent is null)
        {
            actions.Remove(ActionIds.IssueOpenParent);
        }

        if (tab.Detail.Count > 0)
        {
            AddDetailActions(state, tab, actions);
        }
        else if (tab.Rows.Count == 0)
        {
            return null;
        }
        else
        {
            actions.Remove(ActionIds.IssueUnlink);
            actions.AddRange(_rankActions);

            if (SprintBoards.BoardOf(tab.Definition) is not null)
            {
                actions.Add(ActionIds.SprintMoveIssues);
                actions.Add(ActionIds.SprintMoveToBacklog);
            }

            if (BacklogSections.Applies(tab) || HasSubtasks(tab))
            {
                actions.Add(ActionIds.SectionToggle);
            }
        }

        var items = actions.Select(Item).ToList();

        return new ContextMenuState(items, 0);
    }

    private static bool HasSubtasks(TabState tab)
    {
        return tab.Definition.Layout == ViewLayout.Columns
            && ColumnsNavigation.SelectedIssue(tab) is { } card
            && (card.IsListed || card.Subtasks.Count > 0);
    }

    private static void AddDetailActions(AppState state, TabState tab, List<string> actions)
    {
        if (DetailReducer.TargetOf(tab.Detail[^1]) is not { LinkId: not null })
        {
            actions.Remove(ActionIds.IssueUnlink);
        }

        actions.Add(ActionIds.CommentOrder);

        if (CommentEditing.IsOnComment(state))
        {
            actions.Add(ActionIds.CommentDelete);
        }
    }

    /// <summary>
    /// The menu of the active tab and its view: close the tab, the previous tab (only with several tabs), the layout,
    /// jump, help and quit. Null when no tab is open.
    /// </summary>
    public static ContextMenuState? ForView(AppState state)
    {
        if (state.Session.Active is null)
        {
            return null;
        }

        var actions = new List<string> { ActionIds.TabClose };
        var active = state.Session.Tabs.FirstOrDefault(tab => tab.Id == state.Session.Active);

        if (active is not null && SprintBoards.BoardOf(active.Definition) is not null)
        {
            actions.Add(ActionIds.SprintSwitch);
        }

        if (active is { Definition.Source: ViewSourceKind.ScrumBacklog or ViewSourceKind.Sprint })
        {
            actions.Add(ActionIds.SprintCreate);
            actions.Add(ActionIds.SprintEdit);
            actions.Add(ActionIds.SprintStart);
            actions.Add(ActionIds.SprintComplete);
        }

        if (active is { Sprint.Goal: { } goal } && !string.IsNullOrWhiteSpace(goal))
        {
            actions.Add(ActionIds.SprintGoal);
        }

        actions.Add(ActionIds.TabRename);

        if (state.Session.Tabs.Count > 1)
        {
            actions.Add(ActionIds.TabPrevious);
        }

        if (active is not null && active.Definition.Source.IsBoardBased())
        {
            actions.Add(ActionIds.ViewQuickFilter);
            actions.Add(ActionIds.ViewSwimlane);
            if (active.CutoffSource is not null)
            {
                actions.Add(ActionIds.ViewDoneCutoff);
            }
        }

        actions.AddRange(
        [
            ActionIds.ViewLayoutToggle,
            ActionIds.ViewSavedOpen,
            ActionIds.ViewSavedSave,
            ActionIds.IssueJump,
            ActionIds.StatusHistory,
            ActionIds.SettingsOpen,
            ActionIds.StatusDismiss,
            ActionIds.HelpToggle,
            ActionIds.Quit,
        ]);

        return new ContextMenuState(actions.Select(Item).ToList(), 0);
    }

    /// <summary>Moves the highlight, clamped to the items.</summary>
    public static ContextMenuState Move(ContextMenuState menu, int delta)
    {
        return menu with { Selected = Math.Clamp(menu.Selected + delta, 0, menu.Items.Count - 1) };
    }

    private static ContextMenuItem Item(string action)
    {
        var title = ActionCatalog.All.FirstOrDefault(definition => definition.Id == action)?.Title ?? action;

        return new ContextMenuItem(action, title);
    }
}
