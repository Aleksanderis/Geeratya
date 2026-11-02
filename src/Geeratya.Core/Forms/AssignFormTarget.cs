using Geeratya.Core.Writes;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Forms;

/// <summary>
/// Assign one issue: a form with the single searchable field Assignee, opened at once with no metadata to fetch
/// (the people come from a search of the typed text). A value that is left empty means unassigned.
/// </summary>
public sealed record AssignFormTarget(string Key, IssueAssignee? Current) : FormTarget
{
    public override string Title => $"Assign {Key}";

    public override string IssueKey => Key;

    public override WriteRequest Request(IReadOnlyList<FieldChange> changes)
    {
        var chosen = changes.Count > 0 && changes[0].Value.Chosen.Count > 0 ? changes[0] : null;

        if (chosen is null)
        {
            return new AssignRequest(Key, null);
        }

        var accountId = chosen.Value.Chosen[0];
        var name = chosen.Display is { Count: > 0 } display ? display[0] : accountId;

        return new AssignRequest(Key, new IssueAssignee(accountId, name));
    }
}
