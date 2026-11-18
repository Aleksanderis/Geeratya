using Geeratya.Core.Forms;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// What the Details pane of the create form shows for a field: the filled block of the detail's Details pane
/// (<see cref="FieldBlockSpec"/>), the label with its required mark, the value as chips or text, the caret while the
/// field is edited. Pure.
/// </summary>
internal static class CreateFieldBlocks
{
    private const string FromSelection = "from selection";
    private const int Lead = 1;
    private const int DropdownCells = 2;

    /// <summary>The block of the field at <paramref name="index"/>, <paramref name="width"/> columns wide.</summary>
    public static FieldBlockSpec Spec(FormState form, int index, int width)
    {
        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var editing = focused && !form.Navigating;
        var room = Math.Max(1, width - Lead - DropdownCells);

        return new FieldBlockSpec(FieldLabels.Of(field), Items(field, editing, room), Dropdown(field), Note(form, field));
    }

    /// <summary>The field opens a popup (its value is chosen, not typed): the block ends in the dropdown.</summary>
    private static bool Dropdown(FormField field)
    {
        return FormFieldKinds.HasPopup(field.Kind);
    }

    private static string? Note(FormState form, FormField field)
    {
        var fromSelection = field.Id == FormFieldIds.Parent && form.Target is CreateFormTarget { ParentKey: { } parent }
            && field.Value.Chosen is [var chosen] && chosen == parent;

        return fromSelection ? FromSelection : null;
    }

    private static List<IReadOnlyList<StyledSpan>> Items(FormField field, bool editing, int room)
    {
        var look = new FieldLook(room, TextRole.ValueBlock) { Editing = editing, Layout = FieldLayout.Items };

        return [.. FieldRows.Value(field, look).Select(static line => line.Spans)];
    }
}
