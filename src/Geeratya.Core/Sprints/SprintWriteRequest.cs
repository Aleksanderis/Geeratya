using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Sprints;

/// <summary>
/// A write on a sprint itself (create, edit). It names no issue, shows on no row and is not optimistic; when Jira
/// confirmed it, the tabs of the board reload (sprint headers and the sprint list come with the reload).
/// </summary>
public abstract record SprintWriteRequest(long BoardId) : WriteRequest
{
    public override IReadOnlyList<string> Keys => [];

    public override bool ChangesRows => false;

    public override bool ReadsBack => false;

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        return null;
    }

    public override bool IsShownBy(IssueSummary row)
    {
        return true;
    }
}
