using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>One clickable button of a <see cref="ChipButtons"/> row: its label and the target a click takes.</summary>
public readonly record struct ChipButton(string Label, MouseTarget Target);

/// <summary>
/// The one helper for a row of clickable buttons (<c>docs/internal/ui-consistency.md</c>, component 6): the line
/// and the click regions come from the same list, so they cannot drift apart. Buttons are drawn in the key role,
/// three cells apart.
/// </summary>
public static class ChipButtons
{
    /// <summary>The cells between two buttons.</summary>
    public const int Gap = 3;

    /// <summary>The row of buttons as one line.</summary>
    public static StyledLine Line(IReadOnlyList<ChipButton> buttons)
    {
        var spans = new List<StyledSpan>();

        foreach (var button in buttons)
        {
            if (spans.Count > 0)
            {
                spans.Add(new StyledSpan(new string(' ', Gap), TextRole.Body));
            }

            spans.Add(new StyledSpan(button.Label, TextRole.Key));
        }

        return new StyledLine(spans);
    }

    /// <summary>
    /// Notes each button of the row drawn from column <paramref name="left"/> on screen row <paramref name="row"/> in
    /// <paramref name="regions"/> (when not null).
    /// </summary>
    public static void Mark(List<OverlayRegion>? regions, int row, int left, IReadOnlyList<ChipButton> buttons)
    {
        if (regions is null)
        {
            return;
        }

        var column = left;

        foreach (var button in buttons)
        {
            var width = TextWidth.Of(button.Label);

            regions.Add(new OverlayRegion(button.Target, row, column, width, 1, 0));
            column += width + Gap;
        }
    }
}
