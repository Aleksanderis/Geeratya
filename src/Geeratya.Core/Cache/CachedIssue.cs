using Geeratya.Jira.Issues.Models;

namespace Geeratya.Core.Cache;

/// <summary>An issue detail as the cache holds it: the detail and when it came from the network.</summary>
/// <param name="Detail">The issue detail.</param>
/// <param name="SavedAt">When the detail was loaded from Jira.</param>
public sealed record CachedIssue(IssueDetail Detail, DateTimeOffset SavedAt);
