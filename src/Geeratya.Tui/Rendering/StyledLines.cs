using System.Globalization;
using System.Text;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>Layout helpers for styled lines.</summary>
public static class StyledLines
{
    /// <summary>
    /// Cuts or pads a line to exactly <paramref name="width"/> columns without splitting a wide character; padding is a body-role space run.
    /// </summary>
    public static StyledLine Fit(StyledLine line, int width)
    {
        var spans = new List<StyledSpan>();
        var used = 0;

        foreach (var span in line.Spans)
        {
            if (used >= width)
            {
                break;
            }

            var text = TextWidth.Fit(span.Text, width - used);

            if (text.Length > 0)
            {
                spans.Add(span with { Text = text });
                used += TextWidth.Of(text);
            }
        }

        if (used < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used), TextRole.Body));
        }

        return new StyledLine(spans);
    }

    /// <summary>
    /// The part of <paramref name="line"/> from column <paramref name="from"/> up to (not including) column
    /// <paramref name="to"/>, spans and grounds kept. A wide character that straddles an edge becomes a space (never
    /// half a character); no padding is added when the line is shorter.
    /// </summary>
    public static StyledLine Slice(StyledLine line, int from, int to)
    {
        var spans = new List<StyledSpan>();
        var column = 0;

        foreach (var span in line.Spans)
        {
            if (column >= to)
            {
                break;
            }

            var text = new StringBuilder();
            var elements = StringInfo.GetTextElementEnumerator(span.Text);

            while (elements.MoveNext())
            {
                var element = elements.GetTextElement();
                var size = DisplayWidth.OfCluster(element);
                var start = column;

                column += size;

                if (start >= from && column <= to)
                {
                    text.Append(element);
                }
                else if (column > from && start < to && size > 0)
                {
                    text.Append(' ', Math.Min(column, to) - Math.Max(start, from));
                }
            }

            if (text.Length > 0)
            {
                spans.Add(span with { Text = text.ToString() });
            }
        }

        return new StyledLine(spans);
    }
}
