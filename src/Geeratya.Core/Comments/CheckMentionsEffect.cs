using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Comments;

/// <summary>A person named by a mention token: the account id and the name the token shows.</summary>
public sealed record MentionedPerson(string Id, string Name);

/// <summary>
/// The editor opened on a comment that already mentions people: ask Jira which of them cannot see the issue
/// (new mentions only come from the issue-scoped search, so only existing text can hold such a mention). Read only.
/// Answered with <see cref="MentionsChecked"/>; a failed lookup is not answered (the check is a hint, not a gate).
/// </summary>
public sealed record CheckMentionsEffect(
    TabId Tab,
    string Key,
    string CommentId,
    IReadOnlyList<MentionedPerson> People) : Effect;

/// <summary>The people of a comment who did not turn up among those who can see the issue.</summary>
public sealed record MentionsChecked(
    TabId Tab,
    string Key,
    string CommentId,
    IReadOnlyList<MentionedPerson> Unseen) : Forms.FormCommand;
