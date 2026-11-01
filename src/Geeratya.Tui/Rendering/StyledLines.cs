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
}
