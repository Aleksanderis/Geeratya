using Geeratya.Core.Editing;
using Geeratya.Core.Text;

namespace Geeratya.Core.Forms;

/// <summary>
/// The one field renderer (<c>docs/internal/ui-consistency.md</c>, component 3): the value rows of a form field by
/// kind (text, paragraph, choice, labels, person, parent, priority), the caret while it is edited, the empty word,
/// the typed filter and the error. The form box, the create panes, the dropdown under a Details field and the
/// in-place editors all draw a field through it and decide only the layout (width, role, rows). A popup of
/// suggestions is drawn by the pick-list renderer. Pure.
/// </summary>
public static class FieldRows
{
    private const int InitialsCells = 2;
    private const string ParentOpen = "[";
    private const string ParentClose = "]";
    private const string Ellipsis = "…";
    private const string CommaGap = ", ";
    private const string SpaceGap = " ";

    /// <summary>The value rows of <paramref name="field"/> as <paramref name="look"/> says (at least one row).</summary>
    public static List<StyledLine> Value(FormField field, FieldLook look)
    {
        return field.Kind switch
        {
            FormFieldKind.Paragraph => ParagraphValue(field, look),
            _ when FormFieldKinds.HasPopup(field.Kind) => ChosenValue(field, look),
            _ => [TextRow(field, look)],
        };
    }

    /// <summary>
    /// The rows <paramref name="first"/> to <paramref name="first"/> plus <paramref name="count"/> of a paragraph, the
    /// caret in its row while editing: for a presentation that picks its own window (the in-place Description).
    /// </summary>
    public static List<StyledLine> Paragraph(FormField field, FieldLook look, int first, int count)
    {
        var prompt = TextBuffer.FromBack(field.Value.Text, field.Back);
        var rows = field.Value.Text.Split('\n');
        var lines = new List<StyledLine>();

        for (var index = first; index < Math.Min(rows.Length, first + count); index++)
        {
            lines.Add(ParagraphRow(prompt, rows[index], index, look));
        }

        return lines;
    }

    /// <summary>The typed filter (or search text) of a choice, labels or picked field with its caret.</summary>
    public static StyledLine Entry(FormField field, int width, TextRole role)
    {
        return new StyledLine(CaretText.Spans(TextBuffer.FromBack(field.Entry, field.Back), width, role));
    }

    /// <summary>
    /// The error of a field or form as rows (wrapped, <c>! </c> first), indented by <paramref name="indent"/> columns,
    /// for a total width of <paramref name="width"/>.
    /// </summary>
    public static List<StyledLine> Error(string message, int width, int indent = 0)
    {
        var rows = FormErrors.Lines(message, Math.Max(1, width - indent));

        if (indent == 0)
        {
            return rows;
        }

        var pad = new string(' ', indent);

        return [.. rows.Select(row => new StyledLine([new StyledSpan(pad, TextRole.Body), .. row.Spans]))];
    }

    /// <summary>The label of the option <paramref name="id"/> of a choice field (the id when it is not listed).</summary>
    public static string LabelOf(FormField field, string id)
    {
        return field.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id;
    }

    private static StyledLine TextRow(FormField field, FieldLook look)
    {
        var width = Math.Max(1, look.Width);
        var text = field.Value.Text;

        if (look.Editing && text.Length > 0)
        {
            var prompt = TextBuffer.FromBack(text, field.Back);

            if (look.Boxed)
            {
                return CaretText.Field(prompt, width);
            }

            return new StyledLine(CaretText.Spans(prompt, width, look.Role));
        }

        if (text.Length == 0)
        {
            return Empty(field, look);
        }

        return StyledLine.Of(DisplayText.Cut(text.ReplaceLineEndings(SpaceGap), width), look.Role);
    }

    // No typed text: the placeholder of the presentation or the shared empty word, muted, plus the caret cell when edited.
    private static StyledLine Empty(FormField field, FieldLook look)
    {
        var width = Math.Max(1, look.Width);

        if (look.Editing && look.Boxed)
        {
            return CaretText.Field(TextBuffer.Empty, width);
        }

        if (look.Placeholder is { } placeholder)
        {
            var spans = new List<StyledSpan>
            {
                new(DisplayText.Cut(placeholder, Math.Max(1, width - (look.Editing ? 1 : 0))), TextRole.Muted),
            };

            if (look.Editing)
            {
                spans.Add(new StyledSpan(SpaceGap, TextRole.EditCaret));
            }

            return new StyledLine(spans);
        }

        if (look.Editing)
        {
            return new StyledLine(CaretText.Spans(TextBuffer.Empty, width, look.Role));
        }

        var empty = ValueText.Empty(field.Kind == FormFieldKind.User);

        return StyledLine.Of(empty.Text, empty.Role);
    }

    private static List<StyledLine> ParagraphValue(FormField field, FieldLook look)
    {
        var text = field.Value.Text;
        var rows = text.Split('\n');

        if (text.Length == 0)
        {
            return [Empty(field, look)];
        }

        if (!look.Editing && !look.Expanded)
        {
            var head = DisplayText.Cut(rows[0], Math.Max(1, look.Width - (rows.Length > 1 ? 1 : 0)));

            return [StyledLine.Of(rows.Length > 1 ? $"{head}{Ellipsis}" : head, look.Role)];
        }

        if (look.Expanded)
        {
            return Paragraph(field, look, 0, rows.Length);
        }

        var caretRow = TextBuffer.FromBack(text, field.Back).Line;
        var shown = Math.Max(1, look.Rows);
        var first = Math.Clamp(caretRow - shown + 1, 0, Math.Max(0, rows.Length - shown));

        return Paragraph(field, look, first, shown);
    }

    private static StyledLine ParagraphRow(TextBuffer prompt, string row, int index, FieldLook look)
    {
        var width = Math.Max(1, look.Width);

        if (look.Boxed && look.Editing)
        {
            return CaretText.FieldRow(prompt, row, index, width);
        }

        if (!look.Editing)
        {
            return new StyledLine(CaretText.Cut(row, width, look.Role));
        }

        return new StyledLine(CaretText.ParagraphSpans(prompt, row, index, width, look.Role));
    }

    private static List<StyledLine> ChosenValue(FormField field, FieldLook look)
    {
        var items = field.Value.Chosen.Select(id => Item(field, id, look)).ToList();

        if (look.Layout == FieldLayout.Items)
        {
            return ItemRows(field, items, look);
        }

        return Flow(field, items, look);
    }

    // One row per chosen value, and the typed filter as the last row while the field is edited.
    private static List<StyledLine> ItemRows(FormField field, List<IReadOnlyList<StyledSpan>> items, FieldLook look)
    {
        var rows = items.Select(static item => new StyledLine(item)).ToList();

        if (look.Editing && FormFieldKinds.TakesEntry(field.Kind))
        {
            rows.Add(Entry(field, Math.Max(1, look.Width), look.Role));
        }

        if (rows.Count > 0)
        {
            return rows;
        }

        if (field.Kind == FormFieldKind.User)
        {
            return [new StyledLine(ValueText.Person(null, InitialsCells, look.Role))];
        }

        return [Empty(field, look with { Editing = false })];
    }

    // The values joined and wrapped to the width; the typed filter ends the last row, caret in view.
    private static List<StyledLine> Flow(FormField field, List<IReadOnlyList<StyledSpan>> items, FieldLook look)
    {
        var width = Math.Max(1, look.Width);
        var gap = field.Kind is FormFieldKind.Labels or FormFieldKind.MultiSelect ? SpaceGap : CommaGap;
        var rows = new List<List<StyledSpan>>();
        var current = new List<StyledSpan>();
        var used = 0;
        var all = items.Select(item => new FlowItem(item, gap)).ToList();

        if (look.Editing && FormFieldKinds.HasPopup(field.Kind))
        {
            all.Add(new FlowItem(CaretText.Spans(TextBuffer.FromBack(field.Entry, field.Back), width, look.Role), SpaceGap));
        }

        if (all.Count == 0)
        {
            return [Empty(field, look with { Editing = false })];
        }

        foreach (var item in all)
        {
            var size = item.Spans.Sum(static span => DisplayWidth.Of(span.Text));
            var lead = current.Count == 0 ? 0 : DisplayWidth.Of(item.Gap);

            if (current.Count > 0 && used + lead + size > width)
            {
                rows.Add(current);
                current = [];
                used = 0;
                lead = 0;
            }

            if (lead > 0)
            {
                current.Add(new StyledSpan(item.Gap, look.Role));
            }

            current.AddRange(item.Spans);
            used += lead + size;
        }

        rows.Add(current);

        return [.. rows.Select(static row => new StyledLine(row))];
    }

    private static IReadOnlyList<StyledSpan> Item(FormField field, string id, FieldLook look)
    {
        // A value never outgrows the width: the name is cut to what is left after the brackets, chip padding or initials.
        var name = DisplayText.Cut(Clean(LabelOf(field, id)), Math.Max(1, look.Width - InitialsCells - 1));

        if (field.Kind == FormFieldKind.User)
        {
            return ValueText.Person(name, InitialsCells, look.Role);
        }

        if (field.Kind == FormFieldKind.Labels && look.Layout == FieldLayout.Items)
        {
            return [new StyledSpan($" {name} ", LabelChips.For(id))];
        }

        if (field.Kind is FormFieldKind.Labels or FormFieldKind.Parent or FormFieldKind.MultiSelect)
        {
            return [new StyledSpan($"{ParentOpen}{name}{ParentClose}", look.Role)];
        }

        if (field.Id == FormFieldIds.Priority)
        {
            return ValueText.Priority(name, look.Role);
        }

        return [new StyledSpan(name, look.Role)];
    }

    private static string Clean(string text)
    {
        return string.Concat(text.Where(static character => !char.IsControl(character)));
    }

    private sealed record FlowItem(IReadOnlyList<StyledSpan> Spans, string Gap);
}
