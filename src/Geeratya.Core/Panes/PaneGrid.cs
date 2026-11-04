namespace Geeratya.Core.Panes;

/// <summary>
/// The panes a screen shows and where they sit. Data, not code: the wide detail has a sidebar column, the narrow
/// one is a single column, and both are the same type. An empty grid means the screen has no panes.
/// </summary>
/// <param name="Slots">The panes, in focus order (the first is focused when nothing else is).</param>
public sealed record PaneGrid(IReadOnlyList<PaneSlot> Slots)
{
    /// <summary>A grid with no panes.</summary>
    public static PaneGrid Empty { get; } = new([]);

    /// <summary>The slot with this id, or null.</summary>
    public PaneSlot? Find(string id)
    {
        foreach (var slot in Slots)
        {
            if (slot.Id == id)
            {
                return slot;
            }
        }

        return null;
    }
}
