using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>One answer of a question dialog: the key chip (key from the keymap, label) and the target a click takes.</summary>
/// <param name="Chip">The key text and the words of the answer (<c>y</c>, <c>Delete</c>).</param>
/// <param name="Target">What a click on the row means.</param>
public readonly record struct AnswerRow(HintChip Chip, MouseTarget Target);

/// <summary>
/// The answer rows of a Confirm dialog (<c>docs/internal/ui-consistency.md</c>, component 6): one typed
/// <see cref="AnswerRow"/> per answer, drawn as <c>y  Delete</c> with the keys in one aligned column. The lines and
/// the click regions come from the same list, so a row cannot be drawn without being clickable or the reverse.
/// </summary>
public static class AnswerRows
{
    private const int KeyGap = 2;

    /// <summary>The line of each answer: its key padded to the widest key, a gap, then its words in the strong role.</summary>
    public static List<StyledLine> Lines(IReadOnlyList<AnswerRow> answers, int width)
    {
        var keyWidth = answers.Count == 0 ? 0 : answers.Max(static answer => TextWidth.Of(answer.Chip.Key));
        var room = Math.Max(0, width - keyWidth - KeyGap);

        return
        [
            .. answers.Select(answer => new StyledLine(
            [
                new StyledSpan(TextWidth.Pad(answer.Chip.Key, keyWidth), TextRole.Key),
                new StyledSpan(new string(' ', KeyGap), TextRole.Body),
                new StyledSpan(TextWidth.Fit(answer.Chip.Label, room), TextRole.Strong),
            ])),
        ];
    }

    /// <summary>
    /// Notes each answer in <paramref name="regions"/> (when not null): the first at box row
    /// <paramref name="firstRow"/>, each next one on the row below.
    /// </summary>
    public static void Mark(List<OverlayRegion>? regions, BoxPlace place, int firstRow, IReadOnlyList<AnswerRow> answers)
    {
        for (var index = 0; index < answers.Count; index++)
        {
            OverlayBox.Mark(regions, answers[index].Target, place.Top, place.Left, place.Width, firstRow + index, [0]);
        }
    }
}
