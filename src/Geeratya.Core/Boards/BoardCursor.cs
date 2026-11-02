namespace Geeratya.Core.Boards;

/// <summary>
/// Where the selection is on a board in the columns layout: the selected column, and for every column the selected
/// card and the first visible card, so each column remembers its own place (<c>docs/ux-screens.md</c>, "Columns
/// layout"). Indexes count cards of the column, never rows of the tab.
/// </summary>
/// <param name="Column">The selected column.</param>
/// <param name="Cards">The selected card of each column; an empty column holds 0.</param>
/// <param name="Scrolls">The first visible card of each column.</param>
/// <param name="Wanted">
/// The card index the user last chose by moving up or down (vim's curswant). A column change aims at it and clamps
/// it to the target, so the row survives a pass through a short or empty column.
/// </param>
public sealed record BoardCursor(int Column, IReadOnlyList<int> Cards, IReadOnlyList<int> Scrolls, int Wanted)
{
    /// <summary>The selected card of the selected column; 0 when there are no columns.</summary>
    public int Card => Column >= 0 && Column < Cards.Count ? Cards[Column] : 0;
}
