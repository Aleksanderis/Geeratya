using Geeratya.Core.Backlog;
using Geeratya.Core.State;
using Geeratya.Core.Views;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The issue under the cursor, for the status bar: its key and status (<c>GEN-5 In Progress</c>), in a list, backlog
/// or board alike. Pure.
/// </summary>
public static class SelectedIssueText
{
    /// <summary>The text, empty when there is no issue to stand on (nothing loaded, an empty or collapsed section).</summary>
    public static string Text(TabState tab)
    {
        if (tab.Definition.Layout == ViewLayout.Columns)
        {
            return BoardText(tab);
        }

        if (tab.Rows.Count == 0
            || tab.FocusedSprintId is not null
            || (BacklogSections.Applies(tab) && BacklogCursor.OnCollapsedHeader(tab)))
        {
            return string.Empty;
        }

        return Of(tab.Rows[Math.Clamp(tab.Selected, 0, tab.Rows.Count - 1)]);
    }

    private static string BoardText(TabState tab)
    {
        if (ColumnsNavigation.SelectedColumn(tab) is not { } selection || selection.Rows.Count == 0)
        {
            return string.Empty;
        }

        return Of(selection.Rows[Math.Clamp(selection.Index, 0, selection.Rows.Count - 1)]);
    }

    private static string Of(IssueSummary issue)
    {
        return ConnectionText.Sanitize($"{issue.Key} {issue.Status.Name}");
    }
}
