using Geeratya.Core.Cache;
using Geeratya.Core.Views;
using Geeratya.Jira.Comments.Models;
using Geeratya.Jira.Issues.Models;
using Geeratya.Jira.Paging;
using Geeratya.Jira.RemoteLinks.Models;
using Geeratya.Jira.Search.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Core.Detail;

/// <summary>
/// Push an issue on a tab's stack and load it; <paramref name="Row"/> is its list row when there is one.
/// </summary>
public sealed record IssueDetailOpenRequested(TabId Tab, string Key, IssueSummary? Row) : ViewCommand;

/// <summary>Pop the top of a tab's issue stack.</summary>
public sealed record IssueDetailBackRequested(TabId Tab) : ViewCommand;

/// <summary>
/// The issue arrived; applied only while <paramref name="RequestId"/> is the current issue request of a stacked issue.
/// </summary>
public sealed record IssueDetailLoaded(TabId Tab, long RequestId, IssueDetail Issue, DateTimeOffset At) : ViewCommand;

/// <summary>
/// The cache had the issue; applied only while <paramref name="RequestId"/> is current and no issue is shown yet.
/// </summary>
public sealed record IssueDetailCached(TabId Tab, long RequestId, CachedIssue Cached) : ViewCommand;

/// <summary>A comment page arrived; applied only while <paramref name="RequestId"/> is current.</summary>
public sealed record IssueCommentsLoaded(
    TabId Tab,
    long RequestId,
    OffsetPage<IssueComment> Page,
    DateTimeOffset At) : ViewCommand;

/// <summary>The remote links arrived; applied only while <paramref name="RequestId"/> is current.</summary>
public sealed record IssueRemoteLinksLoaded(
    TabId Tab,
    long RequestId,
    IReadOnlyList<RemoteLink> Links,
    DateTimeOffset At) : ViewCommand;

/// <summary>The first page of children arrived; applied only while <paramref name="RequestId"/> is current.</summary>
public sealed record IssueChildrenLoaded(TabId Tab, long RequestId, IssuePage Page, DateTimeOffset At) : ViewCommand;

/// <summary>One of the loads failed; the part is the one whose current request id matches.</summary>
public sealed record IssueDetailFailed(TabId Tab, long RequestId, JiraError Error) : ViewCommand;
