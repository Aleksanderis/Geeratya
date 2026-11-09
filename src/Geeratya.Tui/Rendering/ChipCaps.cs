using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Rounds off filled chips (<c>docs/internal/ux-look.md</c>, "Rounded chips"). A chip is a span in a chip role whose text
/// starts and ends with the padding space; with caps on, the two padding cells become Powerline half circles
/// (U+E0B6, U+E0B4) drawn in the chip's fill colour on the surrounding background, so the chip keeps its width, its mouse
/// cells and its text. Generic: a chip kind takes part by being listed in <see cref="CapRole"/>. Pure.
/// </summary>
public static class ChipCaps
{
    /// <summary>Drawn before the chip text (needs a Powerline or Nerd Font).</summary>
    public const string Left = "";

    /// <summary>Drawn after the chip text.</summary>
    public const string Right = "";

    private const int MinChipLength = 3;

    /// <summary>
    /// The role of the cap of a chip drawn in <paramref name="chip"/>, or null when that role takes no caps.
    /// </summary>
    public static TextRole? CapRole(TextRole chip)
    {
        return chip switch
        {
            TextRole.LabelChip0 => TextRole.LabelCap0,
            TextRole.LabelChip1 => TextRole.LabelCap1,
            TextRole.LabelChip2 => TextRole.LabelCap2,
            TextRole.LabelChip3 => TextRole.LabelCap3,
            TextRole.LabelChip4 => TextRole.LabelCap4,
            _ => null,
        };
    }

    /// <summary>
    /// Caps on the chips of <paramref name="frame"/> (header and body; the status bar has none). The caller decides when
    /// caps may be drawn: a Unicode terminal with colours (a cap is colour on colour, which monochrome cannot show) and
    /// the setting on; without them the plain padded chip stays.
    /// </summary>
    public static StyledFrame Apply(StyledFrame frame)
    {
        return frame with
        {
            Header = [.. frame.Header.Select(Apply)],
            Body = [.. frame.Body.Select(Apply)],
        };
    }

    /// <summary>The line with every chip rounded; the same line when it has none.</summary>
    public static StyledLine Apply(StyledLine line)
    {
        if (!line.Spans.Any(IsChip))
        {
            return line;
        }

        var spans = new List<StyledSpan>(line.Spans.Count + 2);

        for (var index = 0; index < line.Spans.Count; index++)
        {
            var span = line.Spans[index];

            if (!IsChip(span))
            {
                spans.Add(span);

                continue;
            }

            var cap = CapRole(span.Role)!.Value;
            var before = index > 0 ? line.Spans[index - 1].Ground : span.Ground;
            var after = index + 1 < line.Spans.Count ? line.Spans[index + 1].Ground : span.Ground;

            spans.Add(new StyledSpan(Left, cap, before));
            spans.Add(span with { Text = span.Text[1..^1] });
            spans.Add(new StyledSpan(Right, cap, after));
        }

        return new StyledLine(spans);
    }

    private static bool IsChip(StyledSpan span)
    {
        return span.Text.Length >= MinChipLength && span.Text[0] == ' ' && span.Text[^1] == ' ' && CapRole(span.Role) is not null;
    }
}
