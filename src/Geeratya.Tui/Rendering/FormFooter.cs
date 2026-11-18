using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The hint chips of the field form, by what the form is doing (typed <see cref="HintChip"/> values). The box footer
/// and the status line both read <see cref="Chips"/>, in every presentation (box, create panes, Details dropdown).
/// </summary>
public static class FormFooter
{
    private const string CtrlPrefix = "Ctrl-";

    /// <summary>The footer of <paramref name="form"/> as one line.</summary>
    public static StyledLine Line(FormState form, KeymapTable keymap)
    {
        return DialogFooter.Line(Chips(form, keymap));
    }

    /// <summary>The chips of <paramref name="form"/>; a key the keymap does not bind gives null.</summary>
    public static List<HintChip?> Chips(FormState form, KeymapTable keymap)
    {
        if (form.IsChoosingType)
        {
            return
            [
                Move(keymap),
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Choose),
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Close),
            ];
        }

        if (PopupChips(form, keymap) is { } popup)
        {
            return popup;
        }

        if (form.Dropdown is not null)
        {
            return DropdownChips(form, keymap);
        }

        if (form.Pane is not null)
        {
            return
            [
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, HintLabels.Save),
                DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Cancel),
            ];
        }

        return Editing(form, keymap);
    }

    /// <summary>
    /// How many chips at the end stay when the status line is narrow: the way out, and in the create panes the write
    /// too. The others drop from the end.
    /// </summary>
    public static int Kept(FormState form, int count)
    {
        var panes = CreatePanes.Applies(form) && form.Suggest is null && form.Dropdown is null && form.Pane is null;

        return Math.Min(panes ? 2 : 1, count);
    }

    /// <summary>The word of <c>Ctrl-s</c> on <paramref name="form"/>: create for a new issue, else save.</summary>
    public static string SubmitLabel(FormState form)
    {
        return form.Target is CreateFormTarget ? HintLabels.Create : HintLabels.Save;
    }

    /// <summary>
    /// The footer of a form while a popup is open on its focused field (a choice, a person, a label, a parent): one
    /// set for every presentation and every kind, so the box, the create panes and the Details dropdown say the
    /// same (#1021, #1030). The rows keys (move, choose) are there only while the popup has rows; a form with a
    /// navigate state adds its field and write keys. Null when no popup is open.
    /// </summary>
    public static List<HintChip?>? PopupChips(FormState form, KeymapTable keymap)
    {
        if (form.IsChoosingType || form.Navigating || form.Suggest is not { } popup)
        {
            return null;
        }

        var chips = new List<HintChip?>();

        if (popup.Items.Count > 0)
        {
            chips.Add(Move(keymap));
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Choose));
        }

        if (form.Navigable)
        {
            chips.Add(Fields(keymap));
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, SubmitLabel(form)));
        }

        chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, PopupEscape(form, popup)));

        return chips;
    }

    // Esc empties a typed filter of a choice, person, parent or labels field first and, with
    // nothing typed leaves the field (a form with a navigate state) or the form.
    private static string PopupEscape(FormState form, SuggestionState popup)
    {
        var kind = form.Fields[form.Focus].Kind;

        if (popup.Query.Length > 0 && (FormFieldKinds.IsChoice(kind) || FormFieldKinds.TakesEntry(kind)))
        {
            return HintLabels.ClearFilter;
        }

        return form.Navigable ? HintLabels.StopEditing : HintLabels.Close;
    }

    // A dropdown under a Details field: a typed field (the Estimate) saves with Enter, a popup still loading only closes.
    private static List<HintChip?> DropdownChips(FormState form, KeymapTable keymap)
    {
        var focused = form.Focus >= 0 && form.Focus < form.Fields.Count ? form.Fields[form.Focus] : null;
        var close = DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, HintLabels.Close);

        if (focused is not null && !FormFieldKinds.HasPopup(focused.Kind))
        {
            return [DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Save), close];
        }

        return [close];
    }

    private static List<HintChip?> Editing(FormState form, KeymapTable keymap)
    {
        var panes = CreatePanes.Applies(form);
        var chips = new List<HintChip?> { Fields(keymap) };

        if (form.Navigating)
        {
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormNext, HintLabels.Edit));
        }

        // The pane keys act in the navigate state only; typing and choosing keep the Insert rows.
        if (panes && form.Navigating)
        {
            chips.Add(new HintChip(PaneKeys(keymap), HintLabels.Pane));
        }

        if (panes && CreatePanes.CanFold(form))
        {
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormMoreFields, HintLabels.MoreFields));
        }

        if (form.Target is CreateFormTarget create
            && keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormOpenAfter) is { Length: > 0 } open)
        {
            var state = create.OpenAfter ? FormOverlay.OpenAfterOnText : FormOverlay.OpenAfterOffText;

            chips.Add(new HintChip(open, $"open after: {state}"));
        }

        chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormSubmit, SubmitLabel(form)));

        if (form.BrowserUrl is not null && form.Fields.Any(field => !field.IsEditable))
        {
            chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.FormOpenBrowser, HintLabels.Browser));
        }

        chips.Add(DialogFooter.Chip(keymap, InputMode.Insert, ActionIds.Cancel, EscapeLabel(form)));

        return chips;
    }

    // Esc steps out of an edited field of a navigable form, else it cancels the form.
    private static string EscapeLabel(FormState form)
    {
        return form.Navigable && !form.Navigating ? HintLabels.StopEditing : HintLabels.Cancel;
    }

    private static HintChip? Move(KeymapTable keymap)
    {
        return DialogFooter.Pair(
            keymap,
            InputMode.Insert,
            ActionIds.PickerPrevious,
            ActionIds.PickerNext,
            HintLabels.Move);
    }

    private static HintChip? Fields(KeymapTable keymap)
    {
        return DialogFooter.Pair(
            keymap,
            InputMode.Insert,
            ActionIds.FormFieldNext,
            ActionIds.FormFieldPrevious,
            HintLabels.Field);
    }

    // Ctrl-h/j/k/l when the four pane keys are Ctrl plus a letter, else the keys joined by a slash.
    private static string PaneKeys(KeymapTable keymap)
    {
        string[] actions =
        [
            ActionIds.PaneFocusLeft,
            ActionIds.PaneFocusDown,
            ActionIds.PaneFocusUp,
            ActionIds.PaneFocusRight,
        ];
        var keys = actions
            .Select(action => KeyNotation.Display(keymap.FindKeys(InputMode.Normal, action)) ?? string.Empty)
            .ToList();

        if (keys.All(static key => key.Length == CtrlPrefix.Length + 1
            && key.StartsWith(CtrlPrefix, StringComparison.Ordinal)))
        {
            return $"{CtrlPrefix}{string.Join('/', keys.Select(static key => key[^1]))}";
        }

        return string.Join('/', keys);
    }
}
