namespace Geeratya.Jira.Edit.Models;

/// <summary>
/// Who an issue is assigned to. The three cases are distinct on the wire and here: leaving the issue unassigned,
/// letting Jira pick the project's default assignee, and a named account.
/// </summary>
public abstract record AssigneeChange
{
    /// <summary>No assignee (<c>accountId: null</c>).</summary>
    public static AssigneeChange Unassign { get; } = new UnassignedChange();

    /// <summary>Jira picks the project's default assignee (<c>accountId: "-1"</c>); may end up unassigned.</summary>
    public static AssigneeChange Automatic { get; } = new AutomaticChange();

    /// <summary>The account with this id (from the assignable-user search).</summary>
    public static AssigneeChange User(string accountId) => new AccountChange(accountId);

    private AssigneeChange()
    {
    }

    internal sealed record UnassignedChange : AssigneeChange;

    internal sealed record AutomaticChange : AssigneeChange;

    internal sealed record AccountChange(string AccountId) : AssigneeChange;
}
