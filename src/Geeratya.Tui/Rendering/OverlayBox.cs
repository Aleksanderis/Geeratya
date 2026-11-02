using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The frame of a centred box over the body, shared by the overlays (source picker, field form). Every box row
/// replaces the whole body row it sits on, so nothing of the screen behind shows through at its sides.
/// </summary>
public static class OverlayBox
{
    public const char Horizontal = '─';

    private const string Corner = "┌┐└┘";
    private const string Vertical = "│";

    /// <summary>The top edge with <paramref name="title"/> after the first rule character.</summary>
    public static StyledLine Top(int boxWidth, string title)
    {
        var text = $" {title} ";
        var fill = Math.Max(0, boxWidth - 3 - TextWidth.Of(text));

        return StyledLine.Of($"{Corner[0]}{Horizontal}{text}{new string(Horizontal, fill)}{Corner[1]}", TextRole.Muted);
    }

    public static StyledLine Bottom(int boxWidth)
    {
        return StyledLine.Of($"{Corner[2]}{new string(Horizontal, boxWidth - 2)}{Corner[3]}", TextRole.Muted);
    }

    /// <summary>A box row: the content padded to the inner width between two walls and a space.</summary>
    public static StyledLine Wall(StyledLine content, int contentWidth)
    {
        var spans = new List<StyledSpan> { new(Vertical, TextRole.Muted), new(" ", TextRole.Body) };

        spans.AddRange(StyledLines.Fit(content, contentWidth - 2).Spans);
        spans.Add(new StyledSpan(" ", TextRole.Body));
        spans.Add(new StyledSpan(Vertical, TextRole.Muted));

        return new StyledLine(spans);
    }

    /// <summary>
    /// Notes the clickable rows of a box in <paramref name="regions"/> (when not null): the box row at
    /// <paramref name="boxTop"/> + <paramref name="offset"/> + n covers the walls' inner width and stands for
    /// <paramref name="indexes"/>[n] (a negative index is not clickable).
    /// </summary>
    public static void Mark(
        List<OverlayRegion>? regions,
        MouseTarget target,
        int boxTop,
        int left,
        int boxWidth,
        int offset,
        IEnumerable<int> indexes)
    {
        if (regions is null)
        {
            return;
        }

        var row = boxTop + offset;

        foreach (var index in indexes)
        {
            if (index >= 0)
            {
                regions.Add(new OverlayRegion(target, row, left + 1, Math.Max(1, boxWidth - 2), 1, index));
            }

            row++;
        }
    }

    /// <summary>The box row between blank margins, exactly <paramref name="width"/> columns.</summary>
    public static StyledLine Place(StyledLine box, int left, int width)
    {
        var spans = new List<StyledSpan> { new(new string(' ', left), TextRole.Body) };

        spans.AddRange(box.Spans);

        return StyledLines.Fit(new StyledLine(spans), width);
    }
}
