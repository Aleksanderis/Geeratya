using Geeratya.Core.BoardKinds;
using Geeratya.Core.Profiles;
using Geeratya.Jira.Sprints;
using Geeratya.Jira.Sprints.Models;

namespace Geeratya.App;

/// <summary>
/// Saves the board kinds a resolver probed, per profile. The answers are a convenience: a file that cannot be
/// written only means one more probe next run, so a write problem is not reported.
/// </summary>
public sealed class BoardKindSaver(Profile profile, BoardKindStore? store) : IBoardKindObserver
{
    public void Probed(long boardId, BoardKind kind)
    {
        try
        {
            store?.Save(profile, boardId, kind);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Not reported: see the type comment.
        }
    }
}
