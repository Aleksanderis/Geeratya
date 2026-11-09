using Geeratya.Jira.Adf;
using Geeratya.Jira.Search.Models;

namespace Geeratya.Jira.Comments.Models;

/// <summary>One comment on an issue, in the order the caller asked Jira to list them (oldest or newest first).</summary>
/// <param name="Id">The comment id.</param>
/// <param name="Author">Who wrote it; null for a missing account. A deleted or inactive user keeps the name Jira sent.</param>
/// <param name="Body">The body as parsed ADF; empty when Jira sent none or it could not be read.</param>
/// <param name="Created">When it was written; null when unreadable.</param>
/// <param name="Updated">When it was last edited; null when unreadable.</param>
public sealed record IssueComment(
    string Id,
    IssueAssignee? Author,
    AdfDocument Body,
    DateTimeOffset? Created,
    DateTimeOffset? Updated);
