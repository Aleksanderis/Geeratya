using Geeratya.Core.Boards;
using Geeratya.Core.State;
using Geeratya.Core.Text;
using Geeratya.Core.Views;

namespace Geeratya.Tui.Rendering;

public sealed partial class ColumnsBodyRenderer
{
    /// <summary>
    /// The lane strip (#874): every lane as <c>▾ Name n</c> (the one whose columns show, bold) or <c>▸ Name n</c>
    /// (muted), cut around the current one when they do not all fit, with <c>‹</c> and <c>›</c> for the hidden ones.
    /// The cells are those <see cref="LaneStrip.Layout"/> gives, which the mouse uses too.
    /// </summary>
    private static StyledLine LaneStripLine(TabState tab, int width)
    {
        var layout = LaneStrip.Layout(ColumnsNavigation.LanesOf(tab), tab.Lane, width);
        var spans = new List<StyledSpan>();
        var column = 0;

        if (layout.MoreLeft)
        {
            spans.Add(new StyledSpan($"{HiddenLeftMark} ", TextRole.Muted));
            column = 2;
        }

        foreach (var chip in layout.Chips)
        {
            if (chip.Start > column)
            {
                spans.Add(new StyledSpan(new string(' ', chip.Start - column), TextRole.Body));
            }

            spans.Add(new StyledSpan(chip.Text, chip.Current ? TextRole.Heading : TextRole.Muted));
            column = chip.End;
        }

        if (layout.MoreRight && column < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - 1 - column), TextRole.Body));
            spans.Add(new StyledSpan(HiddenRightMark, TextRole.Muted));
        }

        return new StyledLine(spans);
    }

    /// <summary>
    /// Stacked lanes (#891): under the strip, each drawn lane is a header row (<c>▾ Name n</c>, <c>▸</c> when folded to
    /// the header alone, #893; bold for the current lane) and its own framed columns, in the rows <see cref="LaneRegions"/> gives. The body is padded or cut to
    /// <paramref name="height"/> so a host that reports another height than the tab's never gets a short frame.
    /// </summary>
    private static void AddStackedLanes(
        List<StyledLine> lines,
        AppState state,
        TabState tab,
        BodyContext context,
        FlashCards? flash,
        int width,
        int height)
    {
        var current = ColumnsNavigation.CurrentLaneKey(tab);
        var lanes = ColumnsNavigation.LanesOf(tab);

        foreach (var region in LaneRegions.Regions(tab))
        {
            var room = Math.Min(region.Rows, height - lines.Count);

            if (room < LaneRegions.HeaderRows || (room == LaneRegions.HeaderRows && !region.Folded))
            {
                break;
            }

            var lane = lanes.First(candidate => candidate.Key == region.Key);

            lines.Add(LaneHeaderLine(lane, region.Key == current, region.Folded, width));

            if (region.Folded)
            {
                continue;
            }

            lines.AddRange(RenderBoard(state, tab, region, context, flash, width, room - LaneRegions.HeaderRows));
        }

        while (lines.Count < height)
        {
            lines.Add(new StyledLine([new StyledSpan(new string(' ', width), TextRole.Body)]));
        }
    }

    private static StyledLine LaneHeaderLine(SwimlaneLane lane, bool current, bool folded, int width)
    {
        var text = DisplayWidth.Fit(LaneStrip.HeaderLabel(lane, folded), width);

        return new StyledLine([new StyledSpan(text, current ? TextRole.Heading : TextRole.Muted)]);
    }

    /// <summary>The cursor as drawn: no card selected, for a lane that does not hold the cursor.</summary>
    private static BoardCursor Unselected(BoardCursor cursor, bool focused)
    {
        return focused ? cursor : cursor with { Cards = [.. cursor.Cards.Select(static _ => -1)] };
    }
}
