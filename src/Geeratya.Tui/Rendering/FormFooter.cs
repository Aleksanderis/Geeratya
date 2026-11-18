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

        if (PopupChips(form, keymap) is { } popup)
        {
            return popup;
        }

        return Editing(form, keymap, fields);
    }

    // The words of the status bar: Esc steps out of an edited field of a navigable form, else it cancels the form.
    private static string EscapeLabel(FormState form)
    {
        return form.Navigable && !form.Navigating ? HintLabels.StopEditing : HintLabels.Cancel;
    }

    /// <summary>
    /// The footer of a form while a popup with rows is open on its focused field (a choice, a person, a label, a
    /// parent): one set for every presentation and every kind, so the box, the create panes and the Details dropdown
    /// say the same (#1021). Null when no popup with rows is open.
    /// </summary>
    public static List<HintChip?>? PopupChips(FormState form, KeymapTable keymap)
    {
        if (form.IsChoosingType || form.Navigating || form.Suggest is not { Items.Count: > 0 } popup)
        {
            return null;
        }

        var escape = EscapeLabel(form, popup);
        var chips = new List<HintChip?>
        {
            DialogFooter.Pair(
                keymap,
                InputMode.Insert,
                ActionIds.PickerPrevious,
                ActionIds.PickerNext,
                HintLabels.Move),
            DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Choose),
        };

        if (form.Navigable)
        {
            chips.Add(DialogFooter.Pair(
                keymap,
                InputMode.Insert,
                ActionIds.FormFieldNext,
                ActionIds.FormFieldPrevious,
                HintLabels.Field));
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, HintLabels.Save));
        }

        chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, escape));

        return chips;
    }

    // Esc empties a typed filter of a choice or person field first, closes the popup of a label being typed, and with
    // nothing typed leaves the field (a form with a navigate state) or the form.
    private static string EscapeLabel(FormState form, SuggestionState popup)
    {
        var kind = form.Fields[form.Focus].Kind;

        if (popup.Query.Length > 0 && (FormFieldKinds.IsChoice(kind) || FormFieldKinds.IsPicked(kind)))
        {
            return HintLabels.ClearFilter;
        }

        if (popup.Query.Length > 0)
        {
            return HintLabels.Close;
        }

        return form.Navigable ? HintLabels.StopEditing : HintLabels.Close;
    }

    private static List<HintChip?> Editing(FormState form, KeymapTable keymap, HintChip? fields)
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

        chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, EscapeLabel(form)));

        if (form.Target is CreateFormTarget create
            && keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormOpenAfter) is { Length: > 0 } open)
        {
            var state = create.OpenAfter ? FormOverlay.OpenAfterOnText : FormOverlay.OpenAfterOffText;

            chips.Add(new HintChip(open, $"open after: {state}"));
        }

        return chips;
    }
}
