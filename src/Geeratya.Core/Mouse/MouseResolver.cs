using Geeratya.Core.State;
using Geeratya.Core.Views;

namespace Geeratya.Core.Mouse;

/// <summary>Resolves a pointer event to the commands the state layer runs: hit test, then the mouse table.</summary>
public static class MouseResolver
{
    /// <summary>The parts of the mouse table that are wired today; rows of later parts have no effect yet.</summary>
    public const string WiredParts = "AB";

    private static readonly IReadOnlyList<Command> _none = [];

    /// <summary>
    /// The commands for the gesture at the cell, empty when nothing is bound there. A click, double-click or
    /// right-click on a list or an opened issue first places the cursor under the pointer, then runs the action.
    /// </summary>
    public static IReadOnlyList<Command> Resolve(
        MouseContext context,
        int column,
        int row,
        MouseGesture gesture,
        DateTimeOffset at)
    {
        if (MouseHitTester.Locate(context, column, row) is not { } hit)
        {
            return _none;
        }

        if (hit.Target == MouseTarget.Tabs)
        {
            return TabClick(context, hit, gesture);
        }

        var inBody = hit.Target is MouseTarget.Lists or MouseTarget.Detail;

        if (inBody && gesture == MouseGesture.Click)
        {
            return [new PointerPlaced(column, hit.Row, context.Width)];
        }

        if (hit.Target == MouseTarget.Banner && !context.BannerShown)
        {
            return _none;
        }

        if (DefaultMouseMap.Find(hit.Target, gesture) is not { } found || !WiredParts.Contains(found.Part))
        {
            return _none;
        }

        var action = new ActionRequested(found.Action, found.Count, at);

        // The row under the pointer is the target of the action, not the row the cursor happened to be on.
        var placesFirst = inBody && gesture is MouseGesture.DoubleClick or MouseGesture.RightClick;

        return placesFirst ? [new PointerPlaced(column, hit.Row, context.Width, action)] : [action];
    }

    private static IReadOnlyList<Command> TabClick(MouseContext context, MouseHit hit, MouseGesture gesture)
    {
        if (gesture != MouseGesture.Click)
        {
            return _none;
        }

        foreach (var area in context.TabAreas)
        {
            if (hit.Column >= area.Start && hit.Column < area.End)
            {
                return [new TabActivated(area.Tab)];
            }
        }

        return _none;
    }
}
