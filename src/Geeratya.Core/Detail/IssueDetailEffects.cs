using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Detail;

/// <summary>
/// Load one issue; the host answers with <see cref="IssueDetailLoaded"/> or <see cref="IssueDetailFailed"/>.
/// </summary>
public sealed record LoadIssueDetailEffect(TabId Tab, string Key, long RequestId) : Effect;

/// <summary>
/// Load the comment page at <paramref name="StartAt"/>, newest first when <paramref name="NewestFirst"/> (offsets
/// count in that order); the answer is <see cref="IssueCommentsLoaded"/> or a failure.
/// </summary>
public sealed record LoadIssueCommentsEffect(TabId Tab, string Key, int StartAt, long RequestId, bool NewestFirst)
    : Effect;

/// <summary>Load the remote links; the answer is <see cref="IssueRemoteLinksLoaded"/> or a failure.</summary>
public sealed record LoadRemoteLinksEffect(TabId Tab, string Key, long RequestId) : Effect;

/// <summary>
/// Load the first page of children (<c>parent = KEY</c>); the answer is <see cref="IssueChildrenLoaded"/> or a failure.
/// </summary>
public sealed record LoadIssueChildrenEffect(TabId Tab, string Key, long RequestId) : Effect;

/// <summary>
/// Load the fields the account may edit on the issue (editmeta); the answer is <see cref="IssueEditableLoaded"/>. A
/// failure posts nothing: the Details pane then shows no dropdown marks.
/// </summary>
public sealed record LoadEditableFieldsEffect(TabId Tab, string Key) : Effect;

/// <summary>
/// Load the assignees of the sub-tasks of a board card that was expanded (<c>parent = KEY</c>, #911): the stubs in the
/// board payload carry none. The answer is <see cref="SubtaskAssigneesLoaded"/>; a failure posts nothing and the
/// sub-task rows stay without a person.
/// </summary>
public sealed record LoadSubtaskAssigneesEffect(TabId Tab, string ParentKey) : Effect;
