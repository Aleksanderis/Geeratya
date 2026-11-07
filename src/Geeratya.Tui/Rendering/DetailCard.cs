using Geeratya.Core.Detail;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The card the issue detail and the create form sit in when they float over the view (<c>docs/internal/ux-screens.md</c>,
/// "Where it is drawn"): a rounded frame with one column of padding, so the panes never touch the view behind. Pure.
/// </summary>
public static class DetailCard
{
    /// <summary>
    /// Draws <paramref name="panes"/> (the lines for the pane area of <paramref name="box"/>) into the card over
    /// <paramref name="body"/>, the muted view; a box that fills the body has no card and is drawn as it is.
    /// </summary>
    public static List<StyledLine> Draw(
        IReadOnlyList<StyledLine> body,
        IReadOnlyList<StyledLine> panes,
        DetailOverlayGeometry box,
        int width)
    {
        var result = OverlayBox.Inactive(body);
        var cardRows = new List<StyledLine>();

        if (!box.Fills)
        {
            cardRows.Add(OverlayBox.TopRule(box.CardWidth));
        }

        for (var index = 0; index < box.Height; index++)
        {
            var line = index < panes.Count ? panes[index] : StyledLine.Empty;

            cardRows.Add(box.Fills ? StyledLines.Fit(line, box.Width) : OverlayBox.Wall(line, box.CardWidth - 2));
        }

        if (!box.Fills)
        {
            cardRows.Add(OverlayBox.Bottom(box.CardWidth));
        }

        for (var index = 0; index < cardRows.Count && box.CardTop + index < result.Count; index++)
        {
            result[box.CardTop + index] = OverlayBox.Over(
                result[box.CardTop + index],
                cardRows[index],
                box.CardLeft,
                width);
        }

        return result;
    }
}
