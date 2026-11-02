using Geeratya.Core.Forms;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Core.Writes;

/// <summary>
/// Create an issue from the changed fields of a create form. It has no key until Jira answers, so it never conflicts
/// with another write and the form submits once (the form locks while it runs). Not optimistic: there is no row to
/// patch. On success the origin tab reloads, and with <paramref name="OpenAfter"/> the new issue opens in the detail.
/// A lost reply is checked by searching for the issue and is never repeated (a repeat would create a duplicate).
/// </summary>
/// <param name="ProjectKey">The project.</param>
/// <param name="IssueTypeId">The id of the chosen issue type (the name is only for the texts).</param>
/// <param name="IssueTypeName">The name of the type, for the status line.</param>
/// <param name="ParentKey">The parent, or null.</param>
/// <param name="Changes">The fields the user filled in.</param>
/// <param name="OpenAfter">Open the new issue in the detail when it is created.</param>
public sealed record CreateRequest(
    string ProjectKey,
    string IssueTypeId,
    string IssueTypeName,
    string? ParentKey,
    IReadOnlyList<FieldChange> Changes,
    bool OpenAfter) : WriteRequest
{
    public override OperationKind Kind => OperationKind.CreateIssue;

    public override IReadOnlyList<string> Keys => [];

    public override bool CreatesIssue => true;

    /// <summary>The summary the user typed (the form requires it).</summary>
    public string Summary => Changes.FirstOrDefault(change => change.FieldId == FormFieldIds.Summary)?.Value.Text.Trim()
        ?? string.Empty;

    public override string BusyText => "creating";

    public override string StartedText => $"Creating {IssueTypeName} in {ProjectKey}";

    public override string DoneText => $"Created {IssueTypeName} in {ProjectKey}";

    public override string CheckingText => $"Not sure the {IssueTypeName} was created, checking";

    public override string NotAppliedText => $"The {IssueTypeName} was not created";

    public override string ActionText => $"create {IssueTypeName} in {ProjectKey}";

    public override string UnreachableText =>
        $"The {IssueTypeName} may or may not have been created: reload to check, do not create it twice";

    /// <summary>The status line after Jira created the issue.</summary>
    public static string CreatedText(string key)
    {
        return $"Created {key}";
    }

    // ReSharper disable once ReturnTypeCanBeNotNullable (the base says null for writes that are not optimistic)
    public override RowChange? Plan(IssueSummary row)
    {
        return null;
    }

    /// <summary>The issue as read back: same project, type and summary (the search may find an older twin).</summary>
    public override bool IsShownBy(IssueSummary row)
    {
        return string.Equals(row.ProjectKey, ProjectKey, StringComparison.OrdinalIgnoreCase)
            && row.IssueType.Id == IssueTypeId
            && row.Summary.Trim() == Summary;
    }
}
