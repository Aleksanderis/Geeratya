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
        return Fit(line, width, TextRole.Body);
    }

    /// <summary>As <see cref="Fit(StyledLine, int)"/>, with the padding drawn in <paramref name="padding"/>.</summary>
    public static StyledLine Fit(StyledLine line, int width, TextRole padding)
    {
        if (IsExactly(line, width))
        {
            return line;
        }

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
                spans.Add(ReferenceEquals(text, span.Text) ? span : span with { Text = text });
                used += TextWidth.Of(text);
            }
        }

        if (used < width)
        {
            spans.Add(new StyledSpan(new string(' ', width - used), padding));
        }

        return new StyledLine(spans);
    }

    // A line that is already exactly `width` columns with no empty span is returned as is: it is the common case for
    // a line that an earlier stage of the frame build has fitted, and refitting it would only copy it. (A trailing span of only zero-width characters after the full width is kept
    // rather than dropped; it draws nothing.)
    private static bool IsExactly(StyledLine line, int width)
    {
        var used = 0;

        foreach (var span in line.Spans)
        {
            if (span.Text.Length == 0)
            {
                return false;
            }

            used += TextWidth.Of(span.Text);

            if (used > width)
            {
                return false;
            }
        }

        return used == width;
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
            var source = span.Text;

            for (var index = 0; index < source.Length;)
            {
                var length = StringInfo.GetNextTextElementLength(source.AsSpan(index));
                var element = source.AsSpan(index, length);
                var size = DisplayWidth.OfCluster(element);
                var start = column;

                index += length;
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
