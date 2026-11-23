using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Keymap;

/// <summary>
/// What the active tab is, for the lists that show keys (help layout page, which-key popup): the layout
/// the keys are for and whether the tab has sprints. A key that cannot act here is left out, so a Kanban board never
/// lists the sprint keys. <see cref="Everything"/> is the context of a list that shows every key (<c>All actions</c>,
/// the generated reference). Pure.
/// </summary>
public sealed record ActionContext(ActionScope? Layout, bool Sprints)
{
    private const string SprintPrefix = "sprint.";

    /// <summary>No narrowing: every action applies.</summary>
    public static ActionContext Everything { get; } = new(null, true);

    /// <summary>The context of the active tab of <paramref name="state"/>; no tab gives no layout and no sprints.</summary>
    public static ActionContext Of(AppState state)
    {
        var tab = state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == state.Session.Active);

        return new ActionContext(LayoutOf(tab), HasSprints(tab));
    }

    /// <summary>The layout of the tab (an opened issue, columns, the backlog or a list); null without a tab.</summary>
    public static ActionScope? LayoutOf(TabState? tab)
    {
        if (tab is null)
        {
            return null;
        }

        if (tab.Detail.Count > 0)
        {
            return ActionScope.Detail;
        }

        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return ActionScope.Columns;
        }

        var isBacklog = tab.Definition.Source is ViewSourceKind.Backlog or ViewSourceKind.ScrumBacklog;

        return isBacklog ? ActionScope.Backlog : ActionScope.List;
    }

    /// <summary>
    /// True when the tab can have sprints: a Sprint view, the sections of a Scrum backlog, or a board that no Kanban
    /// answer has marked (a Kanban board read its Done cutoff; a plain backlog is what a Kanban answer leaves).
    /// JQL and filter tabs have none.
    /// </summary>
    public static bool HasSprints(TabState? tab)
    {
        return tab?.Definition.Source switch
        {
            ViewSourceKind.Sprint or ViewSourceKind.ScrumBacklog => true,
            ViewSourceKind.Board => tab.CutoffSource is null,
            _ => false,
        };
    }

    /// <summary>True when the action is one of the sprint actions (<c>sprint.*</c>).</summary>
    public static bool IsSprintAction(string action)
    {
        return action.StartsWith(SprintPrefix, StringComparison.Ordinal);
    }

    /// <summary>
    /// True when the action can act here: a sprint action needs sprints, and with a layout known an action tagged for
    /// other layouts only does not apply (an untagged action applies everywhere).
    /// </summary>
    public bool Applies(string action)
    {
        if (IsSprintAction(action) && !Sprints)
        {
            return false;
        }

        var scope = ActionCatalog.ScopeOf(action);

        return Layout is not { } layout || scope == ActionScope.Global || (scope & layout) != 0;
    }
}
