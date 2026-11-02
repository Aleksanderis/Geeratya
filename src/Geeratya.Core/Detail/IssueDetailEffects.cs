using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// Load one issue; the host answers with <see cref="IssueDetailLoaded"/> or <see cref="IssueDetailFailed"/>.
/// </summary>
public sealed record LoadIssueDetailEffect(TabId Tab, string Key, long RequestId) : Effect;

/// <summary>
/// Load the comment page at <paramref name="StartAt"/>; the answer is <see cref="IssueCommentsLoaded"/> or a failure.
/// </summary>
public sealed record LoadIssueCommentsEffect(TabId Tab, string Key, int StartAt, long RequestId) : Effect;

/// <summary>Load the remote links; the answer is <see cref="IssueRemoteLinksLoaded"/> or a failure.</summary>
public sealed record LoadRemoteLinksEffect(TabId Tab, string Key, long RequestId) : Effect;

/// <summary>
/// Load the first page of children (<c>parent = KEY</c>); the answer is <see cref="IssueChildrenLoaded"/> or a failure.
/// </summary>
public sealed record LoadIssueChildrenEffect(TabId Tab, string Key, long RequestId) : Effect;
