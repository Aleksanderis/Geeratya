namespace Geeratya.Core.Panes;

/// <summary>
/// The panes of one tab: the grid, which pane has the focus, which is zoomed and the inner scroll of the panes.
/// Scoped to the tab, kept while it is open and dropped with it (<c>docs/ux-screens.md</c>, "Panes, focus and zoom").
/// The default has no panes, so a screen that does not use panes is not affected.
/// </summary>
/// <param name="Grid">The panes the screen shows now; empty when it has none.</param>
/// <param name="Focused">The id of the focused pane; null until a pane is focused (the first pane then counts).</param>
/// <param name="Zoomed">The id of the zoomed pane, which is also the current section of the strip; null when not zoomed.</param>
/// <param name="Strip">The section strip order while zoomed; empty means the order of the grid.</param>
/// <param name="Scrolls">The inner scroll of the panes that have one, by pane id.</param>
public sealed record PaneState(
    PaneGrid Grid,
    string? Focused = null,
    string? Zoomed = null,
    IReadOnlyList<string>? Strip = null,
    IReadOnlyDictionary<string, PaneScroll>? Scrolls = null)
{
    /// <summary>No panes, nothing focused or zoomed.</summary>
    public static PaneState None { get; } = new(PaneGrid.Empty);

    /// <summary>True when the screen shows panes.</summary>
    public bool HasPanes => Grid.Slots.Count > 0;

    /// <summary>The focused pane: <see cref="Focused"/> when it is in the grid, else the first pane, else null.</summary>
    public string? Current
    {
        get
        {
            if (Focused is { } id && Grid.Find(id) is not null)
            {
                return id;
            }

            return Grid.Slots.Count > 0 ? Grid.Slots[0].Id : null;
        }
    }

    /// <summary>The section strip while zoomed: <see cref="Strip"/> narrowed to existing panes, else the grid order.</summary>
    public IReadOnlyList<string> Sections
    {
        get
        {
            var order = Strip is { Count: > 0 } ? Strip : Grid.Slots.Select(slot => slot.Id).ToList();

            return order.Where(id => Grid.Find(id) is not null).ToList();
        }
    }

    /// <summary>The scroll of a pane, or null when it has none.</summary>
    public PaneScroll? ScrollOf(string id)
    {
        return Scrolls is not null && Scrolls.TryGetValue(id, out var scroll) ? scroll : null;
    }
}
