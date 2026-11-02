using Geeratya.Core.State;

namespace Geeratya.Core.Mouse;

/// <summary>Resolves a pointer event to the command the state layer runs: hit test, then the mouse table.</summary>
public static class MouseResolver
{
    /// <summary>The parts of the mouse table that are wired today; rows of later parts have no effect yet.</summary>
    public const string WiredParts = "A";

    /// <summary>The command for the gesture at the cell, or null when nothing is bound there.</summary>
    public static ActionRequested? Resolve(
        MouseContext context,
        int column,
        int row,
        MouseGesture gesture,
        DateTimeOffset at)
    {
        if (MouseHitTester.Locate(context, column, row) is not { } hit)
        {
            return null;
        }

        if (DefaultMouseMap.Find(hit.Target, gesture) is not { } found || !WiredParts.Contains(found.Part))
        {
            return null;
        }

        return new ActionRequested(found.Action, found.Count, at);
    }
}
