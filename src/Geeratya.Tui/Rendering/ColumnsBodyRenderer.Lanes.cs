using Geeratya.Core.Boards;
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
        var layout = LaneStrip.Layout(tab.Swimlane, tab.Lane, tab.Rows, width);
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
}
