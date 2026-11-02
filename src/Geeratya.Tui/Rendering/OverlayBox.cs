using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The frame of a centred box over the body (source picker, comment editor): top edge with a title, walls with one
/// space of padding, bottom edge, and the placement of a box row between blank margins. Pure.
/// </summary>
public static class OverlayBox
{
    private const string Corner = "┌┐└┘";
    private const char Horizontal = '─';
    private const string Vertical = "│";
    private const int DividerMargin = 2;

    /// <summary>The top edge of a box <paramref name="boxWidth"/> columns wide, with the title set into it.</summary>
    public static StyledLine Top(string title, int boxWidth)
    {
        var text = $" {title} ";
        var fill = Math.Max(0, boxWidth - 3 - TextWidth.Of(text));

        return StyledLine.Of($"{Corner[0]}{Horizontal}{text}{new string(Horizontal, fill)}{Corner[1]}", TextRole.Muted);
    }

    public static StyledLine Bottom(int boxWidth)
    {
        return StyledLine.Of($"{Corner[2]}{new string(Horizontal, boxWidth - 2)}{Corner[3]}", TextRole.Muted);
    }

    /// <summary>A rule across the inside of a box whose content is <paramref name="contentWidth"/> columns wide.</summary>
    public static StyledLine Divider(int contentWidth)
    {
        return StyledLine.Of(new string(Horizontal, Math.Max(0, contentWidth - DividerMargin)), TextRole.Muted);
    }

    /// <summary>A box row: the content padded to the inner width between two walls, with one space of padding.</summary>
    public static StyledLine Wall(StyledLine content, int contentWidth)
    {
        var spans = new List<StyledSpan> { new(Vertical, TextRole.Muted), new(" ", TextRole.Body) };

        spans.AddRange(StyledLines.Fit(content, contentWidth - 2).Spans);
        spans.Add(new StyledSpan(" ", TextRole.Body));
        spans.Add(new StyledSpan(Vertical, TextRole.Muted));

        return new StyledLine(spans);
    }

    /// <summary>The box row between blank margins, exactly <paramref name="width"/> columns.</summary>
    public static StyledLine Place(StyledLine box, int left, int width)
    {
        var spans = new List<StyledSpan> { new(new string(' ', left), TextRole.Body) };

        spans.AddRange(box.Spans);

        return StyledLines.Fit(new StyledLine(spans), width);
    }
}
