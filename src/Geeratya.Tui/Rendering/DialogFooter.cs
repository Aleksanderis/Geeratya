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
