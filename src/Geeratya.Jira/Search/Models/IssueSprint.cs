using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Jira.Search.Models;

/// <summary>
/// A sprint as the <c>Sprint</c> field of an issue lists it. The field is a label: sprint membership comes from the
/// sprint endpoint (<c>docs/jira-layer.md</c>, "Sprint field").
/// </summary>
public sealed record IssueSprint(long Id, string Name, SprintState State)
{
    /// <summary>
    /// The sprint an issue is "in" now: the active one, else a future one, else the last closed one (the field lists
    /// the oldest first). Null for an issue without sprints.
    /// </summary>
    public static IssueSprint? Current(IReadOnlyList<IssueSprint> sprints)
    {
        IssueSprint? future = null;
        IssueSprint? last = null;

        foreach (var sprint in sprints)
        {
            if (sprint.State == SprintState.Active)
            {
                return sprint;
            }

            if (sprint.State == SprintState.Future)
            {
                future ??= sprint;
            }

            last = sprint;
        }

        return future ?? last;
    }
}
