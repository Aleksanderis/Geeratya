using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Core.Detail;

/// <summary>
/// A framed block of the issue detail: a rounded border with the title set into the top edge and one column of padding
/// inside the side edges (<c>docs/ux-screens.md</c>, "Issue detail"). The border characters are Unicode box drawing;
/// the renderer swaps them for ASCII on terminals that cannot show them, one cell for one cell.
/// </summary>
public static class Panel
{
    /// <summary>Columns taken by the side edges and their padding.</summary>
    public const int Chrome = 4;

    /// <summary>Narrowest width that still gets a frame; below it the content is drawn bare under a title line.</summary>
    public const int MinFramedWidth = 12;

    /// <summary>Columns of the top edge besides the title: corner, rule and space before it; space, rule and corner after.</summary>
    private const int TitleChrome = 6;

    public const string TopLeft = "╭";
    public const string TopRight = "╮";
    public const string BottomLeft = "╰";
    public const string BottomRight = "╯";
    public const string Horizontal = "─";
    public const string Vertical = "│";

    /// <summary>The columns available to content inside a panel of <paramref name="width"/>.</summary>
    public static int InnerWidth(int width)
    {
        return width >= MinFramedWidth ? width - Chrome : Math.Max(1, width);
    }

    /// <summary>
    /// Appends the panel to <paramref name="lines"/>. <paramref name="content"/> lines must be at most
    /// <see cref="InnerWidth"/> columns wide.
    /// </summary>
    public static void Add(
        List<StyledLine> lines,
        IReadOnlyList<StyledSpan> title,
        IReadOnlyList<StyledLine> content,
        int width)
    {
        if (width < MinFramedWidth)
        {
            lines.Add(new StyledLine(title));
            lines.AddRange(content);

            return;
        }

        lines.Add(Top(title, width));

        foreach (var line in content)
        {
            lines.Add(Side(line, width));
        }

        lines.Add(Edge(BottomLeft, BottomRight, width));
    }

    private static StyledLine Top(IReadOnlyList<StyledSpan> title, int width)
    {
        var room = width - TitleChrome;
        var spans = new List<StyledSpan> { new($"{TopLeft}{Horizontal} ", TextRole.Muted) };
        var used = 0;

        foreach (var span in title)
        {
            var text = Fit(span.Text, room - used);

            if (text.Length > 0)
            {
                spans.Add(new StyledSpan(text, span.Role));
                used += DisplayWidth.Of(text);
            }
        }

        var fill = room - used;

        spans.Add(new StyledSpan($" {Repeat(Horizontal, fill)}{Horizontal}{TopRight}", TextRole.Muted));

        return new StyledLine(spans);
    }

    private static StyledLine Side(StyledLine content, int width)
    {
        var padding = Math.Max(0, width - Chrome - DisplayWidth.Of(content.PlainText));
        var spans = new List<StyledSpan>(content.Spans.Count + 3) { new($"{Vertical} ", TextRole.Muted) };

        spans.AddRange(content.Spans);
        spans.Add(new StyledSpan(new string(' ', padding), TextRole.Body));
        spans.Add(new StyledSpan($" {Vertical}", TextRole.Muted));

        return new StyledLine(spans);
    }

    private static StyledLine Edge(string left, string right, int width)
    {
        return StyledLine.Of($"{left}{Repeat(Horizontal, width - 2)}{right}", TextRole.Muted);
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
