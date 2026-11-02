namespace Geeratya.Jira.Create.Models;

/// <summary>
/// An issue to create. <see cref="Description"/> is plain text written as ADF (one paragraph per line);
/// <see cref="ParentKey"/> sets the parent in both project kinds; <see cref="Fields"/> holds the other fields the
/// create metadata offered, by field id. With <see cref="DescriptionIsMarkdown"/> the description is read as
/// Markdown-like text (what the form's editor offers) and converted to ADF, otherwise as plain lines.
/// </summary>
public sealed record NewIssue(
    string ProjectKey,
    string IssueTypeId,
    string Summary,
    string? Description = null,
    string? ParentKey = null,
    IReadOnlyDictionary<string, CreateFieldValue>? Fields = null,
    bool DescriptionIsMarkdown = false);
