using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// A pane of the issue detail: a rounded border of its own with the title set into the top edge and one column of
/// padding inside the side edges (<c>docs/ux-screens.md</c>, "Pane layout for the issue detail"). The border
/// characters are Unicode box drawing;
/// the renderer swaps them for ASCII on terminals that cannot show them, one cell for one cell.
/// </summary>
public static class Panel
{
    /// <summary>Columns taken by the side edges and their padding.</summary>
    public const int Chrome = 4;

    /// <summary>Narrowest width that still gets a frame; below it the content is drawn bare under a title.</summary>
    public const int MinFramedWidth = 12;

    /// <summary>Top edge columns besides the chips: corner and rule each side.</summary>
    private const int EdgeChrome = 4;

    private const string TopLeft = "╭";
    private const string TopRight = "╮";
    private const string BottomLeft = "╰";
    private const string BottomRight = "╯";
    public const string Horizontal = "─";
    public const string Vertical = "│";

    /// <summary>The columns available to content inside a panel of <paramref name="width"/>.</summary>
    public static int InnerWidth(int width)
    {
        return width >= MinFramedWidth ? width - Chrome : Math.Max(1, width);
    }

    /// <summary>
    /// Appends the pane to <paramref name="lines"/>: its own rounded top edge with the title chip (and the dim
    /// <paramref name="right"/> chip), the content between side borders and its own bottom edge. Panes are stacked
    /// without a gap, so nothing is framed twice. <paramref name="content"/> lines must be at most
    /// <see cref="InnerWidth"/> columns wide. Below <see cref="MinFramedWidth"/> the pane is a bare title line and
    /// its content.
    /// </summary>
    public static void Add(
        List<StyledLine> lines,
        IReadOnlyList<StyledSpan> title,
        IReadOnlyList<StyledLine> content,
        int width,
        IReadOnlyList<StyledSpan>? right = null)
    {
        if (width < MinFramedWidth)
        {
            lines.Add(new StyledLine(title));
            lines.AddRange(content);

            return;
        }

        lines.Add(TopEdge(title, right ?? [], width));

        foreach (var line in content)
        {
            lines.Add(Side(line, width));
        }

        lines.Add(BottomEdge(width));
    }

    /// <summary>
    /// The top edge: <c>╭─ Title ─────── right ─╮</c>. The chips are the spans between the border spans (the renderer
    /// restyles them for focus); the right chip is dropped when it does not fit.
    /// </summary>
    public static StyledLine TopEdge(
        IReadOnlyList<StyledSpan> title,
        IReadOnlyList<StyledSpan> right,
        int width)
    {
        var room = width - EdgeChrome;
        var left = Chip(title, room);
        var leftWidth = left.Sum(static span => DisplayWidth.Of(span.Text));
        var rightChip = right.Count == 0 ? [] : Chip(right, room - leftWidth - 1);
        var rightWidth = rightChip.Sum(static span => DisplayWidth.Of(span.Text));
        var fill = room - leftWidth - rightWidth;
        var spans = new List<StyledSpan> { new($"{TopLeft}{Horizontal}", TextRole.Border) };

        spans.AddRange(left);

        if (fill > 0)
        {
            spans.Add(new StyledSpan(Repeat(Horizontal, fill), TextRole.Border));
        }

        spans.AddRange(rightChip);
        spans.Add(new StyledSpan($"{Horizontal}{TopRight}", TextRole.Border));

        return new StyledLine(spans);
    }

    /// <summary>The bottom edge, <c>╰───╯</c>.</summary>
    public static StyledLine BottomEdge(int width)
    {
        var rule = Repeat(Horizontal, Math.Max(0, width - 2));

        return new StyledLine([new StyledSpan($"{BottomLeft}{rule}{BottomRight}", TextRole.Border)]);
    }

    /// <summary>
    /// The spans padded with one space each side, cut to <paramref name="room"/> cells; empty when too tight.
    /// </summary>
    private static List<StyledSpan> Chip(IReadOnlyList<StyledSpan> spans, int room)
    {
        var fitted = new List<StyledSpan>();

        if (room < 3)
        {
            return fitted;
        }

        var used = 2;

        fitted.Add(new StyledSpan(" ", TextRole.Muted));

        foreach (var span in spans)
        {
            var text = Fit(span.Text, room - used);

            if (text.Length > 0)
            {
                fitted.Add(new StyledSpan(text, span.Role));
                used += DisplayWidth.Of(text);
            }
        }

        fitted.Add(new StyledSpan(" ", TextRole.Muted));

        return fitted;
    }

    private static StyledLine Side(StyledLine content, int width)
    {
        var padding = Math.Max(0, width - Chrome - DisplayWidth.Of(content.PlainText));
        var spans = new List<StyledSpan>(content.Spans.Count + 3) { new($"{Vertical} ", TextRole.Border) };

        spans.AddRange(content.Spans);
        spans.Add(new StyledSpan(new string(' ', padding), TextRole.Body));
        spans.Add(new StyledSpan($" {Vertical}", TextRole.Border));

        return new StyledLine(spans);
    }

    private static string Repeat(string text, int count)
    {
        return string.Concat(Enumerable.Repeat(text, Math.Max(0, count)));
    }

    /// <summary>The longest prefix of <paramref name="text"/> that fits in <paramref name="room"/> cells.</summary>
    private static string Fit(string text, int room)
    {
        if (room <= 0)
        {
            return string.Empty;
        }

        if (DisplayWidth.Of(text) <= room)
        {
            return text;
        }

        var builder = new StringBuilder();
        var used = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            var size = DisplayWidth.Of(rune);

            if (used + size > room - 1)
            {
                break;
            }

            builder.Append(rune.ToString());
            used += size;
        }

        return builder.Append('…').ToString();
    }
}
