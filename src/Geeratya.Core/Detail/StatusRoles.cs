using Geeratya.Core.Text;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Detail;

/// <summary>The text role of a status, by its category.</summary>
internal static class StatusRoles
{
    public static TextRole Of(IssueStatus status)
    {
        return status.CategoryKey switch
        {
            StatusCategoryKeys.New => TextRole.StatusToDo,
            StatusCategoryKeys.Indeterminate => TextRole.StatusInProgress,
            StatusCategoryKeys.Done => TextRole.StatusDone,
            _ => TextRole.Body,
        };
    }
}
