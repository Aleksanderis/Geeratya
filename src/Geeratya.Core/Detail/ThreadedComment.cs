using Geeratya.Jira.Comments.Models;

namespace Geeratya.Core.Detail;

/// <summary>A comment placed in a thread.</summary>
/// <param name="Comment">The comment.</param>
/// <param name="Depth">How many replies deep it is: 0 for a top-level comment, 1 for a reply to one.</param>
public sealed record ThreadedComment(IssueComment Comment, int Depth);
