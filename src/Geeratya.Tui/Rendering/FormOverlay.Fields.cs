using Geeratya.Core.Forms;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

public static partial class FormOverlay
{
    private static List<StyledLine> Block(FormState form, int index, int valueWidth)
    {
        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var marker = focused ? ">" : " ";
        var required = field.Required ? " *" : string.Empty;
        var readOnly = field.IsEditable ? string.Empty : $"  ({ReadOnlyText})";
        var role = focused ? TextRole.Strong : TextRole.FieldLabel;
        var state = focused && form.Navigable ? (form.Navigating ? "  [navigate]" : "  [editing]") : string.Empty;
        var lines = new List<StyledLine> { StyledLine.Of($"{marker} {field.Label}{required}{readOnly}{state}", role) };

        lines.AddRange(ValueLines(field, focused && !form.Navigating, form, valueWidth));

        if (field.Error is not null)
        {
            lines.Add(StyledLine.Of($"{new string(' ', Indent)}! {field.Error}", TextRole.Error));
        }

        return lines;
    }

    private static List<StyledLine> ValueLines(FormField field, bool focused, FormState form, int valueWidth)
    {
        if (!field.IsEditable)
        {
            return [Indented(form.BrowserUrl ?? ReadOnlyText, TextRole.Muted)];
        }

        return field.Kind switch
        {
            FormFieldKind.Paragraph => ParagraphLines(field, focused, valueWidth),
            FormFieldKind.Select or FormFieldKind.MultiSelect => ChoiceLines(field, focused, form, valueWidth),
            FormFieldKind.Labels => [.. LabelLines(field, focused, valueWidth), .. PopupLines(focused, form, valueWidth)],
            FormFieldKind.User or FormFieldKind.Parent => PickedLines(field, focused, form, valueWidth),
            _ => [TextLine(field, focused, valueWidth)],
        };
    }

    private static StyledLine TextLine(FormField field, bool focused, int valueWidth)
    {
        if (focused)
        {
            return Indented(Caretted(new PromptText(field.Value.Text, field.Back), valueWidth), TextRole.Editable);
        }

        return Indented(field.Value.Text.Length == 0 ? EmptyText : field.Value.Text, TextRole.Editable);
    }

    /// <summary>The text cut around its caret to fit, with the caret mark drawn in it (<c>_</c> at the end, <c>|</c> inside).</summary>
    private static string Caretted(PromptText prompt, int width)
    {
        var window = PromptWindow.Of(prompt, width);

        return $"{window.Before}{window.Caret}{window.After}";
    }

    private static List<StyledLine> ParagraphLines(FormField field, bool focused, int valueWidth)
    {
        var rows = field.Value.Text.Split('\n');
        var lines = new List<StyledLine>();

        if (!focused)
        {
            var head = rows[0].Length == 0 ? EmptyText : rows[0];

            return [Indented(rows.Length > 1 ? $"{head}…" : head, TextRole.Editable)];
        }

        var prompt = new PromptText(field.Value.Text, field.Back);
        var caretRow = prompt.Text[..prompt.Caret].Count(character => character == '\n');
        var first = Math.Clamp(caretRow - ParagraphRows + 1, 0, Math.Max(0, rows.Length - ParagraphRows));
        var shown = Math.Min(ParagraphRows, rows.Length - first);

        for (var index = first; index < first + shown; index++)
        {
            lines.Add(Indented(ParagraphRow(prompt, rows[index], index, caretRow, valueWidth), TextRole.Editable));
        }

        return lines;
    }

    // The row with the caret gets the caret mark; the other rows are cut to the width.
    private static string ParagraphRow(PromptText prompt, string row, int index, int caretRow, int valueWidth)
    {
        if (index != caretRow)
        {
            return PromptWindow.Of(new PromptText(row, 0), valueWidth).Before;
        }

        var lineStart = prompt.Text[..prompt.Caret].LastIndexOf('\n') + 1;
        var column = prompt.Caret - lineStart;

        return Caretted(new PromptText(row, row.Length - column), valueWidth);
    }

    private static StyledLine ChosenLine(FormField field)
    {
        var labels = field.Value.Chosen.Select(id => LabelOf(field, id)).ToArray();

        return Indented(labels.Length == 0 ? EmptyText : string.Join(", ", labels), TextRole.Editable);
    }

    /// <summary>The labels as tokens wrapped to the box; the buffer being typed ends the last line, caret in view.</summary>
    private static List<StyledLine> LabelLines(FormField field, bool focused, int valueWidth)
    {
        var tokens = field.Value.Chosen.Select(label => $"[{LabelOf(field, label)}]").ToList();

        if (focused)
        {
            tokens.Add(Caretted(new PromptText(field.Entry, field.Back), valueWidth));
        }

        if (tokens.Count == 0)
        {
            return [Indented(EmptyText, TextRole.Editable)];
        }

        var rows = new List<string>();
        var current = string.Empty;

        foreach (var token in tokens)
        {
            var joined = current.Length == 0 ? token : $"{current} {token}";

            if (current.Length > 0 && TextWidth.Of(joined) > valueWidth)
            {
                rows.Add(current);
                joined = token;
            }

            current = joined;
        }

        rows.Add(current);

        return [.. rows.Select(row => Indented(row, TextRole.Editable))];
    }

    private static List<StyledLine> PickedLines(FormField field, bool focused, FormState form, int valueWidth)
    {
        if (!focused)
        {
            return [ChosenLine(field)];
        }

        return [.. LabelLines(field, true, valueWidth), .. PopupLines(true, form, valueWidth)];
    }

    /// <summary>The suggestion popup under the focused field: ranked rows, the cursor row marked, a status line.</summary>
    private static List<StyledLine> PopupLines(bool focused, FormState form, int valueWidth)
    {
        if (!focused || form.Suggest is not { } popup || popup.FieldIndex != form.Focus)
        {
            return [];
        }

        var width = Math.Max(1, valueWidth - 2 - PopupMargin);
        var margin = new string(' ', PopupMargin);
        var lines = new List<StyledLine>();
        var cursor = Math.Clamp(popup.Cursor, -1, Math.Max(0, popup.Items.Count - 1));
        var first = PopupFirst(popup);
        var shown = Math.Min(PopupRows, Math.Max(0, popup.Items.Count - first));

        foreach (var index in Enumerable.Range(first, shown))
        {
            var text = TextWidth.Fit(SuggestionText.Row(popup.Items[index]), width);
            var highlighted = index == cursor;
            var pointer = highlighted ? ">" : " ";

            lines.Add(Indented($"{margin}{pointer} {text}", highlighted ? TextRole.Selected : TextRole.Body));
        }

        if (PopupStatus(popup) is { } status)
        {
            lines.Add(Indented($"{margin}  {TextWidth.Fit(status.Text, width)}", status.Role));
        }

        return lines;
    }

    private static PopupNotice? PopupStatus(SuggestionState popup)
    {
        return popup.Status switch
        {
            SuggestionStatus.Loading => new PopupNotice(SearchingText, TextRole.Loading),
            SuggestionStatus.Failed => new PopupNotice($"{LookupFailedText}: {popup.Error}", TextRole.Error),
            SuggestionStatus.Loaded when popup.Items.Count == 0 && popup.Query.Length == 0
                => new PopupNotice(NoMoreText, TextRole.Muted),
            SuggestionStatus.Loaded when popup.Items.Count == 0 => new PopupNotice(NoMatchText, TextRole.Muted),
            _ => null,
        };
    }

    // A choice shows its chosen values like chips and, while focused, the typed filter and the suggestion popup.
    private static List<StyledLine> ChoiceLines(FormField field, bool focused, FormState form, int valueWidth)
    {
        if (!focused)
        {
            return [ChosenLine(field)];
        }

        return [.. LabelLines(field, true, valueWidth), .. PopupLines(true, form, valueWidth)];
    }

    private static string LabelOf(FormField field, string id)
    {
        return field.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id;
    }
}
