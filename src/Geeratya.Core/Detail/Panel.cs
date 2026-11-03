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

    /// <summary>Narrowest width that still gets a frame; below it the content is drawn bare under a title.</summary>
    public const int MinFramedWidth = 12;

    /// <summary>Top edge columns besides the title: corner, rule, space before it; space, rule, corner after.</summary>
    private const int TitleChrome = 6;

    public const string JoinLeft = "├";
    public const string JoinRight = "┤";
    public const string Horizontal = "─";
    public const string Vertical = "│";

    /// <summary>The title spans cut to the room of a top edge of <paramref name="width"/> columns.</summary>
    public static IReadOnlyList<StyledSpan> FitTitle(IReadOnlyList<StyledSpan> title, int width)
    {
        var room = width - TitleChrome;
        var spans = new List<StyledSpan>(title.Count);
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

        return spans;
    }

    /// <summary>The columns available to content inside a panel of <paramref name="width"/>.</summary>
    public static int InnerWidth(int width)
    {
        return width >= MinFramedWidth ? width - Chrome : Math.Max(1, width);
    }

    /// <summary>
    /// Appends the panel to <paramref name="lines"/>. <paramref name="content"/> lines must be at most
    /// <see cref="InnerWidth"/> columns wide. The panels of a document share the one frame the renderer draws around
    /// the visible window: the <paramref name="first"/> panel has no edge of its own (its title goes into the top
    /// edge of the frame) and each later one starts with the shared edge that carries its title, <c>├─ Title ─┤</c>.
    /// </summary>
    public static void Add(
        List<StyledLine> lines,
        IReadOnlyList<StyledSpan> title,
        IReadOnlyList<StyledLine> content,
        int width,
        bool first)
    {
        if (width < MinFramedWidth)
        {
            lines.Add(new StyledLine(title));
            lines.AddRange(content);

            return;
        }

        if (!first)
        {
            lines.Add(Divider(title, width));
        }

        foreach (var line in content)
        {
            lines.Add(Side(line, width));
        }
    }

    private static StyledLine Divider(IReadOnlyList<StyledSpan> title, int width)
    {
        var room = width - TitleChrome;
        var spans = new List<StyledSpan>
        {
            new($"{JoinLeft}{Horizontal}", TextRole.Border),
            new(" ", TextRole.Muted),
        };
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

        spans.Add(new StyledSpan(" ", TextRole.Muted));
        spans.Add(new StyledSpan($"{Repeat(Horizontal, fill)}{Horizontal}{JoinRight}", TextRole.Border));

        return new StyledLine(spans);
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
