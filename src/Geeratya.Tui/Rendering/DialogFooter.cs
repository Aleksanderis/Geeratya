using Geeratya.Core.Keymap;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The footer row of a dialog, built from typed <see cref="HintChip"/> values instead of strings
/// (<c>docs/internal/ui-consistency.md</c>, component 6): the key from the keymap in the key role, the label from
/// <see cref="HintLabels"/> muted. An action without a key gives no chip, so a footer never prints an empty key.
/// </summary>
public static class DialogFooter
{
    /// <summary>The spaces between two chips of a dialog footer.</summary>
    public const string Gap = "   ";

    /// <summary>The chip of <paramref name="action"/>, or null when it has no key in <paramref name="mode"/>.</summary>
    public static HintChip? Chip(KeymapTable keymap, InputMode mode, string action, string label)
    {
        return keymap.FindKeyLabel(mode, action) is { Length: > 0 } key ? new HintChip(key, label) : null;
    }

    /// <summary>
    /// One chip for two actions that share a label (<c>Down/Up move</c>), or the one that has a key; null when
    /// neither has.
    /// </summary>
    public static HintChip? Pair(KeymapTable keymap, InputMode mode, string first, string second, string label)
    {
        var keys = new[] { keymap.FindKeyLabel(mode, first), keymap.FindKeyLabel(mode, second) }
            .Where(static key => key is { Length: > 0 })
            .ToList();

        return keys.Count == 0 ? null : new HintChip(string.Join('/', keys), label);
    }

    /// <summary>
    /// The footer of <paramref name="surface"/> in <paramref name="width"/> columns: the chips of the status line, and
    /// when they do not fit the same rule drops them from the end before the kept way out (the status line's rule).
    /// </summary>
    public static StyledLine Fitted(SurfaceChips surface, int width)
    {
        var chips = surface.Chips.ToList();
        var line = LineOf(chips);

        while (TextWidth.Of(line.PlainText) > width && chips.Count > surface.Kept)
        {
            chips.RemoveAt(chips.Count - surface.Kept - 1);
            line = LineOf(chips);
        }

        return line;
    }

    /// <summary>
    /// The footer of <paramref name="chips"/> that keeps the last one (the way out) when the line has only
    /// <paramref name="width"/> columns.
    /// </summary>
    public static StyledLine Fitted(IEnumerable<HintChip?> chips, int width)
    {
        return Fitted(SurfaceChips.KeepLast(chips), width);
    }

    /// <summary>The footer as one line of chips that all have a key.</summary>
    public static StyledLine LineOf(IEnumerable<HintChip> chips)
    {
        return Line(chips.Select(static chip => (HintChip?)chip));
    }

    /// <summary>The footer as one line: chips in the key and muted roles, <see cref="Gap"/> between them.</summary>
    public static StyledLine Line(IEnumerable<HintChip?> chips)
    {
        var spans = new List<StyledSpan>();

        foreach (var chip in chips.OfType<HintChip>())
        {
            if (spans.Count > 0)
            {
                spans.Add(new StyledSpan(Gap, TextRole.Muted));
            }

            spans.Add(new StyledSpan(chip.Key, TextRole.Key));
            spans.Add(new StyledSpan($" {chip.Label}", TextRole.Muted));
        }

        return new StyledLine(spans);
    }
}
