using Geeratya.Jira.Rank.Models;
using Geeratya.Jira.Writes.Models;

namespace Geeratya.Jira.Sprints.Models;

/// <summary>
/// What a move into a sprint or to the backlog did, over all chunks of up to 50 keys. Every requested key is in
/// exactly one list. A whole call that Jira refused (closed sprint, no permission, rate limit) stops the run:
/// its keys are <see cref="Rejected"/> with the message, the keys of later chunks are <see cref="NotAttempted"/>
/// and <see cref="Error"/> holds the reason. A timeout or lost reply puts the chunk in <see cref="Unknown"/>:
/// reload before retrying.
/// </summary>
public sealed record SprintMoveOutcome(
    IReadOnlyList<string> Moved,
    IReadOnlyList<RankItemResult> Rejected,
    IReadOnlyList<string> Unknown,
    IReadOnlyList<string> NotAttempted,
    WriteError? Error)
{
    /// <summary>True when every requested issue was moved.</summary>
    public bool AllMoved => Rejected.Count == 0 && Unknown.Count == 0 && NotAttempted.Count == 0;
}
