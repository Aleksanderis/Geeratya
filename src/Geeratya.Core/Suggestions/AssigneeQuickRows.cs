using Geeratya.Jira.Suggestions.Models;

namespace Geeratya.Core.Suggestions;

/// <summary>
/// The two fixed rows on top of the assign picker's search while its field is empty: the signed-in account and
/// "Unassigned". They are ordinary suggestions (the popup and its keys stay one path); the id and detail mark them.
/// </summary>
public static class AssigneeQuickRows
{
    /// <summary>The detail that marks the row of the signed-in account (shown as <c>Me (name)</c>).</summary>
    public const string MeDetail = "me";

    /// <summary>The row that clears the assignee: an empty id means no person.</summary>
    public static readonly Suggestion Unassigned = new(string.Empty, "Unassigned", null, false);

    public static Suggestion Me(string accountId, string displayName)
    {
        return new Suggestion(accountId, displayName, MeDetail, false);
    }
}
