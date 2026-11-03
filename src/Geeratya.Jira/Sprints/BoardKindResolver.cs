using System.Collections.Concurrent;
using Geeratya.Jira.Boards.Models;
using Geeratya.Jira.Sprints.Models;
using Geeratya.Jira.Transport;

namespace Geeratya.Jira.Sprints;

/// <summary>
/// Tells Scrum boards from Kanban boards. <c>scrum</c> and <c>kanban</c> are taken from the board type. A
/// <c>simple</c> (team-managed) or unknown board gets one probe of the sprint list: 200 is Scrum, the 400 "The board
/// does not support sprints" is Kanban. Any 400 counts as Kanban (the probe sends nothing else Jira could reject). Any other failure is returned and not
/// remembered. Probed answers are
/// remembered for this resolver (one per connection); the caller may seed it from a per-profile store with
/// <see cref="Remember"/> and hears of each new probed answer through <c>onProbed</c> (not of seeded ones).
/// </summary>
public sealed class BoardKindResolver(SprintClient sprints, Action<long, BoardKind>? onProbed = null)
{
    private readonly ConcurrentDictionary<long, BoardKind> _known = new();

    public async Task<JiraResult<BoardKind>> ResolveAsync(
        BoardSummary board,
        CancellationToken cancellationToken,
        JiraPriority priority = JiraPriority.User)
    {
        switch (board.Type)
        {
            case BoardType.Scrum:
                return JiraResults.Success(BoardKind.Scrum);
            case BoardType.Kanban:
                return JiraResults.Success(BoardKind.Kanban);
        }

        if (_known.TryGetValue(board.Id, out var known))
        {
            return JiraResults.Success(known);
        }

        var probeQuery = new SprintQuery(null, 1) { Priority = priority };
        var probe = await sprints.ListSprintsAsync(board.Id, probeQuery, 0, cancellationToken);

        if (probe.IsSuccess)
        {
            return Remembered(board.Id, BoardKind.Scrum);
        }

        return probe.Error.Kind == JiraErrorKind.BadRequest
            ? Remembered(board.Id, BoardKind.Kanban)
            : JiraResults.Failure<BoardKind>(probe.Error);
    }

    /// <summary>The remembered kind of a board, or null when it was never probed.</summary>
    public BoardKind? Recall(long boardId) => _known.TryGetValue(boardId, out var kind) ? kind : null;

    /// <summary>Seeds an answer persisted from an earlier run, so the probe is skipped.</summary>
    public void Remember(long boardId, BoardKind kind) => _known[boardId] = kind;

    private JiraResult<BoardKind> Remembered(long boardId, BoardKind kind)
    {
        _known[boardId] = kind;
        onProbed?.Invoke(boardId, kind);

        return JiraResults.Success(kind);
    }
}
