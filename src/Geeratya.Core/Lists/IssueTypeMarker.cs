using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Lists;

/// <summary>The one-cell type marker of a list row (<c>docs/ux-screens.md</c>, "List layout").</summary>
public static class IssueTypeMarker
{
    public const char Epic = 'E';
    public const char Story = 'S';
    public const char Task = 'T';
    public const char Bug = 'B';
    public const char Subtask = 's';
    public const char Other = '?';

    private const string StoryName = "Story";
    private const string TaskName = "Task";
    private const string BugName = "Bug";

    /// <summary>
    /// Epic and sub-task come from the hierarchy level and the sub-task flag, which are the same in company-managed
    /// and team-managed projects; level-0 types are told apart by name, anything else is <see cref="Other"/>.
    /// </summary>
    public static char For(IssueTypeSummary type)
    {
        if (type.IsSubtask || type.HierarchyLevel < 0)
        {
            return Subtask;
        }

        if (type.HierarchyLevel > 0)
        {
            return Epic;
        }

        if (string.Equals(type.Name, StoryName, StringComparison.OrdinalIgnoreCase))
        {
            return Story;
        }

        if (string.Equals(type.Name, TaskName, StringComparison.OrdinalIgnoreCase))
        {
            return Task;
        }

        return string.Equals(type.Name, BugName, StringComparison.OrdinalIgnoreCase) ? Bug : Other;
    }
}
