using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Issues.Models;

/// <summary>
/// An attachment's metadata. Attachments are never fetched or rendered: the UI shows name, type and size and opens
/// the issue in the browser. <see cref="ContentUrl"/> needs credentials and is kept for reference only.
/// </summary>
/// <param name="Id">The attachment id.</param>
/// <param name="FileName">The file name, any Unicode.</param>
/// <param name="MimeType">The media type; empty when Jira sent none.</param>
/// <param name="Size">The size in bytes.</param>
/// <param name="Created">When it was added; null when unreadable.</param>
/// <param name="Author">Who added it; null for a deleted or missing account.</param>
/// <param name="ContentUrl">Jira's download URL; null when absent.</param>
public sealed record IssueAttachment(
    string Id,
    string FileName,
    string MimeType,
    long Size,
    DateTimeOffset? Created,
    IssueAssignee? Author,
    string? ContentUrl);
