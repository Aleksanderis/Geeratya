using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The footer chips of the field form, by what the form is doing (typed <see cref="HintChip"/> values).
/// </summary>
public static class FormFooter
{
    /// <summary>The footer of <paramref name="form"/> as one line.</summary>
    public static StyledLine Line(FormState form, KeymapTable keymap)
    {
        return DialogFooter.Line(Chips(form, keymap));
    }

    private static List<HintChip?> Chips(FormState form, KeymapTable keymap)
    {
        var close = DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Close);
        var move = DialogFooter.Pair(
            keymap,
            InputMode.Insert,
            ActionIds.PickerPrevious,
            ActionIds.PickerNext,
            HintLabels.Move);
        var fields = DialogFooter.Pair(
            keymap,
            InputMode.Insert,
            ActionIds.FormFieldNext,
            ActionIds.FormFieldPrevious,
            HintLabels.Field);

        if (form.IsChoosingType)
        {
            return [move, DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Choose), close];
        }

        if (form.Suggest is { Source.Kind: SuggestionKind.Options, Items.Count: > 0 } choice)
        {
            var escape = choice.Query.Length == 0 ? HintLabels.Close : HintLabels.ClearFilter;

            return
            [
                move,
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Choose),
                fields,
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, escape),
            ];
        }

        if (form.Suggest is { Items.Count: > 0 })
        {
            return [move, ChoosingKeys(keymap), close];
        }

        return Editing(form, keymap, fields, close);
    }

    // Tab and Enter both take the highlighted row of a popup of suggestions.
    private static HintChip? ChoosingKeys(KeymapTable keymap)
    {
        var next = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormFieldNext);
        var accept = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormNext);
        var keys = string.Join('/', new[] { next, accept }.Where(static key => key is { Length: > 0 }));

        return keys.Length == 0 ? null : new HintChip(keys, HintLabels.Choose);
    }

    private static List<HintChip?> Editing(FormState form, KeymapTable keymap, HintChip? fields, HintChip? close)
    {
        var chips = new List<HintChip?> { fields };

        if (form.Navigating)
        {
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Edit));
        }

        chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, HintLabels.Save));

        if (form.BrowserUrl is not null && form.Fields.Any(field => !field.IsEditable))
        {
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormOpenBrowser, HintLabels.Browser));
        }

        chips.Add(close);

        if (form.Target is CreateFormTarget create
            && keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormOpenAfter) is { Length: > 0 } open)
        {
            var state = create.OpenAfter ? FormOverlay.OpenAfterOnText : FormOverlay.OpenAfterOffText;

            chips.Add(new HintChip(open, $"open after: {state}"));
        }

        return chips;
    }
}
