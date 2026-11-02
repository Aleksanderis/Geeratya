using Geeratya.Core.Comments;
using Geeratya.Core.Keymap;
using Geeratya.Core.State;

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
    ];

    /// <summary>The menu for the cursor of the active tab, null when there is no issue under it.</summary>
    public static ContextMenuState? For(AppState state)
    {
        var tab = state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == id) : null;

        if (tab is null)
        {
            return null;
        }

        var actions = new List<string>(_issueActions);

        if (tab.Detail.Count > 0)
        {
            if (CommentEditing.IsOnComment(state))
            {
                actions.Add(ActionIds.CommentDelete);
            }
        }
        else if (tab.Rows.Count == 0)
        {
            return null;
        }
        else
        {
            actions.AddRange(_rankActions);
        }

        var items = actions.Select(Item).ToList();

        return new ContextMenuState(items, 0);
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
