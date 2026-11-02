using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Lists;

/// <summary>The colour role of an issue type on a board card; follows <see cref="IssueTypeMarker"/>.</summary>
public static class IssueTypeRole
{
    public static TextRole For(IssueTypeSummary type)
    {
        return IssueTypeMarker.For(type) switch
        {
            IssueTypeMarker.Epic => TextRole.TypeEpic,
            IssueTypeMarker.Story => TextRole.TypeStory,
            IssueTypeMarker.Bug => TextRole.TypeBug,
            IssueTypeMarker.Task => TextRole.TypeTask,
            IssueTypeMarker.Subtask => TextRole.TypeSubtask,
            _ => TextRole.Body,
        };
    }
}
