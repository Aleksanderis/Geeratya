namespace Geeratya.Core.Boards;

/// <summary>
/// Which cards a column draws and on which rows, for cards of different height: from the first card in view as many
/// whole cards as fit. The renderer, the hit test and the flash labeler place cards through here. Pure.
/// </summary>
public static class BoardColumnLayout
{
    /// <summary>
    /// The whole cards that fit in <paramref name="rows"/> rows from card <paramref name="first"/>, in a column cell of
    /// <paramref name="cellWidth"/> cells. Rows under the last whole card stay empty.
    /// </summary>
    public static IReadOnlyList<PlacedCard> Place(BoardColumnGroup group, int first, int cellWidth, int rows)
    {
        var placed = new List<PlacedCard>();
        var top = 0;

        for (var card = Math.Max(0, first); card < group.Rows.Count; card++)
        {
            var part = PartOf(group, card);
            var cardRows = BoardCardPlan.Rows(group.Rows[card], part, cellWidth);

            if (top + cardRows.Count > rows)
            {
                break;
            }

            placed.Add(new PlacedCard(card, top, part, cardRows));
            top += cardRows.Count;
        }

        return placed;
    }

    /// <summary>Where the card sits in the shared frame of a parent and its children.</summary>
    public static CardGroupPart PartOf(BoardColumnGroup group, int card)
    {
        var nested = group.IsNested(card);
        var more = group.IsNested(card + 1);

        if (nested)
        {
            return more ? CardGroupPart.Middle : CardGroupPart.Last;
        }

        return more ? CardGroupPart.First : CardGroupPart.None;
    }

    /// <summary>The placed card that covers <paramref name="row"/> (counted from the first card row), or null.</summary>
    public static PlacedCard? At(IReadOnlyList<PlacedCard> placed, int row)
    {
        foreach (var card in placed)
        {
            if (row >= card.Top && row < card.Top + card.Rows.Count)
            {
                return card;
            }
        }

        return null;
    }
}
