using Geeratya.Jira.Sprints.Models;

namespace Geeratya.Jira.Sprints;

/// <summary>Hears of each board kind a <see cref="BoardKindResolver"/> found out by probing the board.</summary>
public interface IBoardKindObserver
{
    /// <summary>Called once for each new probed answer (not for seeded ones).</summary>
    void Probed(long boardId, BoardKind kind);
}
