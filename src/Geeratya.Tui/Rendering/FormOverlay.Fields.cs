using Geeratya.Core.Forms;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

public static partial class FormOverlay
{
    /// <summary>
    /// One field as a labelled box (<see cref="FieldBox"/>, the frame of the create form's boxes): the label with the
    /// required mark in the top edge, the value rows inside the walls, then the suggestion rows and the error line
    /// under the box. The focused box has the focus border and a filled label; a read-only field and a field with an
    /// error have the plain border (the error shows in its line under the box). The state of the form is in the status
    /// line, not in the label.
    /// </summary>
    private static List<StyledLine> Block(FormState form, int index, int valueWidth)
    {
        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var readOnly = field.IsEditable ? string.Empty : $"  ({ReadOnlyText})";
        var title = new StyledSpan($"{FieldLabels.Of(field)}{readOnly}", TextRole.Heading);
        var width = Math.Max(6, valueWidth + Indent);
        var lines = FieldBox.Build([title], ValueLines(field, focused, form, valueWidth), width, focused);

        if (focused && PickerBox.Draw(form, width) is { } picker)
        {
            lines.AddRange(picker.Lines);

            return lines;
        }

        if (field.Error is not null)
        {
            lines.AddRange(FieldRows.Error(field.Error, valueWidth + Indent, Indent));
        }

        return lines;
    }

    private static List<StyledLine> ValueLines(FormField field, bool focused, FormState form, int valueWidth)
    {
        if (!field.IsEditable)
        {
            return [StyledLine.Of(form.BrowserUrl ?? ReadOnlyText, TextRole.Muted)];
        }

        var look = new FieldLook(valueWidth) { Editing = focused && !form.Navigating, Rows = ParagraphRows };

        return FieldRows.Value(field, look);
    }
}
