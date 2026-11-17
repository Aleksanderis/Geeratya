using Geeratya.Core.Forms;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

public static partial class FormOverlay
{
    private const string BoxTopStart = "┌─";
    private const string BoxTopEnd = "┐";
    private const string BoxBottomStart = "└";
    private const string BoxBottomEnd = "┘";
    private const string BoxWallStart = "│ ";
    private const string BoxWallEnd = " │";

    /// <summary>
    /// One field as a labelled box: the label (with the required mark and state) in the top edge, the value rows
    /// inside the walls, then the suggestion rows and the error line under the box. The focused box is accent
    /// (its label starts with the <c>&gt;</c> mark, so focus shows without colour), a read-only field is dimmed.
    /// </summary>
    private static List<StyledLine> Block(FormState form, int index, int valueWidth)
    {
        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var frame = !field.IsEditable ? TextRole.Muted : focused ? TextRole.OverlayFrame
            : field.Error is not null ? TextRole.Error : TextRole.Muted;
        var marker = focused ? "> " : string.Empty;
        var note = focused && FormShell.StateNote(form) is { } state ? $"  {state}" : string.Empty;
        var required = field.Required ? " *" : string.Empty;
        var readOnly = field.IsEditable ? string.Empty : $"  ({ReadOnlyText})";
        var labelRole = focused ? TextRole.Strong : field.IsEditable ? TextRole.FieldLabel : TextRole.Muted;
        var width = Math.Max(6, valueWidth + Indent);
        var lines = new List<StyledLine> { BoxTop(width, $"{marker}{field.Label}{required}{readOnly}{note}", labelRole, frame) };

        foreach (var value in ValueLines(field, focused, form, valueWidth))
        {
            lines.Add(BoxRow(value, width, frame));
        }

        lines.Add(StyledLine.Of($"{BoxBottomStart}{new string(OverlayBox.Horizontal, width - 2)}{BoxBottomEnd}", frame));

        if (field.IsEditable)
        {
            lines.AddRange(PopupLines(focused, form, valueWidth));
        }

        if (field.Error is not null)
        {
            lines.AddRange(FieldRows.Error(field.Error, valueWidth + Indent, Indent));
        }

        return lines;
    }

    private static StyledLine BoxTop(int width, string label, TextRole labelRole, TextRole frame)
    {
        var text = TextWidth.Fit($" {label} ", Math.Max(0, width - 4));
        var fill = Math.Max(0, width - 3 - TextWidth.Of(text));

        return new StyledLine(
        [
            new StyledSpan(BoxTopStart, frame),
            new StyledSpan(text, labelRole),
            new StyledSpan($"{new string(OverlayBox.Horizontal, fill)}{BoxTopEnd}", frame),
        ]);
    }

    private static StyledLine BoxRow(StyledLine value, int width, TextRole frame)
    {
        var spans = new List<StyledSpan> { new(BoxWallStart, frame) };

        spans.AddRange(StyledLines.Fit(value, width - 4).Spans);
        spans.Add(new StyledSpan(BoxWallEnd, frame));

        return new StyledLine(spans);
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

    /// <summary>The suggestion popup under the focused field: ranked rows, the cursor row marked, a status line.</summary>
    private static List<StyledLine> PopupLines(bool focused, FormState form, int valueWidth)
    {
        if (!focused || form.Suggest is not { } popup || popup.FieldIndex != form.Focus)
        {
            return [];
        }

        var width = Math.Max(1, valueWidth - 2 - PopupMargin);
        var margin = new string(' ', Indent + PopupMargin);

        return [.. PickListRenderer.Popup(popup, width, margin).Select(static row => row.Line)];
    }
}
