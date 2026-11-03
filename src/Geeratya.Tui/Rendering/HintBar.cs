using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A row of key hint chips on the bar (<c>docs/ux-look.md</c>, section 5): the key bold in the key role, the label
/// muted, two spaces between chips. The caller decides which chips fit; this only measures and draws them.
/// </summary>
public static class HintBar
{
    /// <summary>The spaces between two chips.</summary>
    public const string Gap = "  ";

    /// <summary>The cells <paramref name="chips"/> take with the gaps between them; zero for none.</summary>
    public static int Width(IReadOnlyList<HintChip> chips)
    {
        if (chips.Count == 0)
        {
            return 0;
        }

        return chips.Sum(chip => chip.Width) + (Gap.Length * (chips.Count - 1));
    }

    /// <summary>The spans that draw <paramref name="chips"/>: key, space, label, gap.</summary>
    public static IReadOnlyList<StyledSpan> Spans(IReadOnlyList<HintChip> chips)
    {
        var spans = new List<StyledSpan>(chips.Count * 4);

        for (var index = 0; index < chips.Count; index++)
        {
            if (index > 0)
            {
                spans.Add(new StyledSpan(Gap, TextRole.BarText));
            }

            spans.Add(new StyledSpan(chips[index].Key, TextRole.BarKey));
            spans.Add(new StyledSpan($" {chips[index].Label}", TextRole.BarMuted));
        }

        return spans;
    }
}
