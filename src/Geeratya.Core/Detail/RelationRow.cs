using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>One row of a <see cref="RelationTable"/>.</summary>
/// <param name="Key">The issue key.</param>
/// <param name="Summary">The summary.</param>
/// <param name="StatusName">The status name, empty when Jira sent none.</param>
/// <param name="StatusRole">The role of the status (its category colour).</param>
/// <param name="Target">What Enter on the row opens; null for a row that is plain text (a malformed key).</param>
/// <param name="Assignee">
/// The assignee's name; null or blank is unassigned (when <paramref name="AssigneeKnown"/>).
/// </param>
/// <param name="AssigneeKnown">
/// Whether the assignee is known; false leaves the cell blank instead of saying "Unassigned".
/// </param>
/// <param name="Prefix">Text in front of the summary, such as the direction of a link (<c>blocks</c>).</param>
internal sealed record RelationRow(
    string Key,
    string Summary,
    string StatusName,
    TextRole StatusRole,
    DetailTarget? Target,
    string? Assignee = null,
    bool AssigneeKnown = false,
    string Prefix = "");
