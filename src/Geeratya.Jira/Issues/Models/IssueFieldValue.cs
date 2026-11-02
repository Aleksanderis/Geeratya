using Geeratya.Jira.Adf;

namespace Geeratya.Jira.Issues.Models;

/// <summary>
/// The current value of one editable field of an issue, in the shape the edit form needs. A field Jira holds no value
/// for is present with every member empty; a field the issue read did not return is absent from the result.
/// </summary>
/// <param name="Text">Text, number or date text (<c>yyyy-MM-dd</c>); empty when none.</param>
/// <param name="Ids">The ids of the chosen option, component or version values.</param>
/// <param name="Document">The rich text value; null when empty or not rich text.</param>
/// <param name="Moment">The date-time value; null when empty or not a date-time.</param>
public sealed record IssueFieldValue(
    string Text,
    IReadOnlyList<string> Ids,
    AdfDocument? Document,
    DateTimeOffset? Moment)
{
    public static IssueFieldValue Empty { get; } = new(string.Empty, [], null, null);
}
