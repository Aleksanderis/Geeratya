using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Views;

namespace Geeratya.Core.State;

/// <summary>Turns a drag of a card into the move its key would make; pure.</summary>
internal static class CardDrag
{
    private const string NeighbourText = "Drop the card on the next or previous column.";

    /// <summary>The state after the drop: nothing changes unless the card landed on a neighbouring column.</summary>
    public static ReduceResult Drop(AppState state, CardDragged drag)
    {
        var none = new ReduceResult(state, []);
        var tab = state.Session.Active is { } id
            ? state.Session.Tabs.FirstOrDefault(candidate => candidate.Id == id)
            : null;

        if (state.Mode != InputMode.Normal
            || tab is not { Detail.Count: 0 }
            || tab.Definition.Layout != ViewLayout.Columns)
        {
            return none;
        }

        if (ColumnsNavigation.DragSteps(tab, drag.Width, drag.FromColumn, drag.FromRow, drag.ToColumn) is not { } steps
            || steps == 0)
        {
            return none;
        }

        if (Math.Abs(steps) > 1)
        {
            return new ReduceResult(state with { Message = NeighbourText }, []);
        }

        var placed = ViewReducer.PlaceCursor(state, new PointerPlaced(drag.FromColumn, drag.FromRow, drag.Width));

        if (placed is null)
        {
            return none;
        }

        var moved = TransitionForms.MoveCard(placed.State, steps);

        return new ReduceResult(moved.State, [.. placed.Effects, .. moved.Effects]);
    }
}
