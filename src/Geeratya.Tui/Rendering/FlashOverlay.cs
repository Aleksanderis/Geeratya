using System.Globalization;
using Geeratya.Core.Flash;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Draws the flash jump on a list row: the matched text strong, and the label letter over the character after it
///. The row keeps its width: a wide character under the label becomes the letter plus blanks.
/// </summary>
public static class FlashOverlay
{
    /// <summary>The row with the match and the label of <paramref name="label"/> drawn on it.</summary>
    public static StyledLine Apply(StyledLine line, FlashLabel label)
    {
        var result = new List<StyledSpan>(line.Spans.Count + 4);
        var column = 0;

        foreach (var span in line.Spans)
        {
            var clusters = StringInfo.GetTextElementEnumerator(span.Text);

            while (clusters.MoveNext())
            {
                var cluster = clusters.GetTextElement();
                var width = DisplayWidth.OfCluster(cluster);

                if (column == label.LabelColumn && label.HasLetter)
                {
                    var blanks = new string(' ', Math.Max(0, width - 1));

                    Add(result, $"{label.Letter}{blanks}", TextRole.OverlayChip, span.Ground);
                }
                else
                {
                    var inMatch = column >= label.MatchColumn && column < label.MatchColumn + label.MatchWidth;

                    Add(result, cluster, inMatch ? TextRole.Strong : span.Role, span.Ground);
                }

                column += width;
            }
        }

        return new StyledLine(result);
    }

    private static void Add(List<StyledSpan> spans, string text, TextRole role, SpanGround ground)
    {
        if (spans.Count > 0 && spans[^1].Role == role && spans[^1].Ground == ground)
        {
            spans[^1] = spans[^1] with { Text = spans[^1].Text + text };

            return;
        }

        spans.Add(new StyledSpan(text, role, ground));
    }
}
