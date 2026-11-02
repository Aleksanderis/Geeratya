using System.Globalization;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Text;
using Geeratya.Jira.Transitions.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The field form box (<c>docs/ux-screens.md</c>, "Field form overlay"): one block per field (label, value or
/// options, error) in a centred box over the body, the focused field marked with <c>&gt;</c>, a key footer. Pure.
/// The focused block scrolls into view; a field the form cannot edit is listed read-only with the browser link.
/// </summary>
public static class FormOverlay
{
    private const string LoadingText = "Loading fields…";
    private const string SavingText = "Saving…";
    private const string DiscardText = "Discard the changes? (y/n)";
    private const string ReadOnlyText = "edit in browser";
    private const string EmptyText = "-";
    private const string Caret = "_";
    private const int MaxBoxWidth = 100;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 5;
    private const int FooterHeight = 2;
    private const int FooterMinimumInner = 8;
    private const int TopGap = 1;
    private const int Indent = 4;
    private const int OptionRows = 5;
    private const int ParagraphRows = 4;

    /// <summary>Draws the open form over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        FormState form,
        KeymapTable keymap,
        int width,
        int height)
    {
        if (form is { IsLoading: true, Target.ShowsWhileLoading: false })
        {
            return body;
        }

        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, MaxBoxWidth) : width;

        if (boxWidth < 8 || height < MinimumBoxHeight)
        {
            return body;
        }

        var content = Content(form, boxWidth - 4 - Indent);
        var inner = Math.Min(height - TopGap, content.Lines.Count + FooterHeight + 2) - 2;
        var footer = inner >= FooterMinimumInner ? FooterHeight : 0;
        var box = BoxLines(form, content, keymap, boxWidth, inner, footer);
        var top = Math.Min(TopGap, height - box.Count);
        var left = (width - boxWidth) / 2;
        var result = body.ToList();

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Place(box[index], left, width);
        }

        return result;
    }

    /// <summary>The one-line status text of the open form: the state, or the error of the focused field.</summary>
    public static string StatusText(FormState form)
    {
        if (form.DiscardPending)
        {
            return DiscardText;
        }

        if (form.IsSaving)
        {
            return SavingText;
        }

        if (form.IsLoading)
        {
            return LoadingText;
        }

        if (form.TopError is not null)
        {
            return $"! {form.TopError}";
        }

        var focused = form.Focus < form.Fields.Count ? form.Fields[form.Focus] : null;

        return focused?.Error is { } error ? $"! {focused.Label}: {error}" : form.Target.Title;
    }

    private static List<StyledLine> BoxLines(
        FormState form,
        FormContent content,
        KeymapTable keymap,
        int boxWidth,
        int inner,
        int footer)
    {
        var contentWidth = boxWidth - 2;
        var area = Math.Max(0, inner - footer);
        var first = FirstVisible(content, area);
        var lines = new List<StyledLine> { OverlayBox.Top(boxWidth, form.Target.Title) };

        foreach (var line in content.Lines.Skip(first).Take(area))
        {
            lines.Add(OverlayBox.Wall(line, contentWidth));
        }

        while (lines.Count < inner + 1 - footer)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        if (footer > 0)
        {
            var rule = StyledLine.Of(new string(OverlayBox.Horizontal, contentWidth - 2), TextRole.Muted);

            lines.Add(OverlayBox.Wall(rule, contentWidth));
            lines.Add(OverlayBox.Wall(StyledLine.Of(FooterText(form, keymap), TextRole.Muted), contentWidth));
        }

        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    private static int FirstVisible(FormContent content, int area)
    {
        var end = content.FocusStart + content.FocusLength;

        if (area <= 0 || end <= area)
        {
            return 0;
        }

        return Math.Min(content.FocusStart, end - area);
    }

    private static string FooterText(FormState form, KeymapTable keymap)
    {
        var next = keymap.FindKeys(InputMode.Insert, ActionIds.FormFieldNext);
        var previous = keymap.FindKeys(InputMode.Insert, ActionIds.FormFieldPrevious);
        var submit = keymap.FindKeys(InputMode.Insert, ActionIds.FormSubmit);
        var cancel = keymap.FindKeys(InputMode.Insert, ActionIds.Cancel);

        if (form.DiscardPending)
        {
            var yes = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmYes);
            var no = keymap.FindKeys(InputMode.Confirm, ActionIds.ConfirmNo);

            return $"{yes} discard   {no}/{cancel} keep editing";
        }

        var browser = form.BrowserUrl is not null && form.Fields.Any(field => !field.IsEditable)
            ? $"   {keymap.FindKeys(InputMode.Insert, ActionIds.FormOpenBrowser)} browser"
            : string.Empty;

        return $"{next}/{previous} field   {submit} save{browser}   {cancel} cancel";
    }

    private static FormContent Content(FormState form, int valueWidth)
    {
        var lines = new List<StyledLine>();
        var focusStart = 0;
        var focusLength = 0;

        AddNotices(lines, form);

        for (var index = 0; index < form.Fields.Count; index++)
        {
            var block = Block(form, index, valueWidth);

            if (index == form.Focus)
            {
                focusStart = lines.Count;
                focusLength = block.Count;
            }

            lines.AddRange(block);
        }

        return new FormContent(lines, focusStart, focusLength);
    }

    private static void AddNotices(List<StyledLine> lines, FormState form)
    {
        if (form.IsLoading)
        {
            lines.Add(StyledLine.Of(LoadingText, TextRole.Loading));
        }

        if (form.IsSaving)
        {
            lines.Add(StyledLine.Of(SavingText, TextRole.Loading));
        }

        if (form.TopError is not null)
        {
            lines.Add(StyledLine.Of($"! {form.TopError}", TextRole.Error));
        }

        if (form.DiscardPending)
        {
            lines.Add(StyledLine.Of(DiscardText, TextRole.Key));
        }
    }

    private static List<StyledLine> Block(FormState form, int index, int valueWidth)
    {
        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var marker = focused ? ">" : " ";
        var required = field.Required ? " *" : string.Empty;
        var readOnly = field.IsEditable ? string.Empty : $"  ({ReadOnlyText})";
        var role = focused ? TextRole.Strong : TextRole.FieldLabel;
        var lines = new List<StyledLine> { StyledLine.Of($"{marker} {field.Label}{required}{readOnly}", role) };

        lines.AddRange(ValueLines(field, focused, form.BrowserUrl, valueWidth));

        if (field.Error is not null)
        {
            lines.Add(StyledLine.Of($"{new string(' ', Indent)}! {field.Error}", TextRole.Error));
        }

        return lines;
    }

    private static List<StyledLine> ValueLines(FormField field, bool focused, string? browserUrl, int valueWidth)
    {
        if (!field.IsEditable)
        {
            return [Indented(browserUrl ?? ReadOnlyText, TextRole.Muted)];
        }

        return field.Kind switch
        {
            FormFieldKind.Paragraph => ParagraphLines(field, focused, valueWidth),
            FormFieldKind.Select or FormFieldKind.MultiSelect => focused ? OptionLines(field) : [ChosenLine(field)],
            FormFieldKind.Labels => LabelLines(field, focused, valueWidth),
            _ => [TextLine(field, focused, valueWidth)],
        };
    }

    private static StyledLine TextLine(FormField field, bool focused, int valueWidth)
    {
        var text = field.Value.Text.Length == 0 && !focused ? EmptyText : field.Value.Text;

        return Indented(focused ? Tail($"{text}{Caret}", valueWidth) : text, TextRole.Editable);
    }

    /// <summary>The end of the text that fits, so the caret stays in view; a cut text starts with an ellipsis.</summary>
    private static string Tail(string text, int width)
    {
        if (TextWidth.Of(text) <= width || width < 2)
        {
            return text;
        }

        var starts = StringInfo.ParseCombiningCharacters(text);
        var start = starts.Length;

        while (start > 0 && TextWidth.Of(text[starts[start - 1]..]) + 1 <= width)
        {
            start--;
        }

        return $"…{text[(start < starts.Length ? starts[start] : text.Length)..]}";
    }

    private static List<StyledLine> ParagraphLines(FormField field, bool focused, int valueWidth)
    {
        var rows = field.Value.Text.Split('\n');
        string[] shown = focused ? [.. rows.TakeLast(ParagraphRows)] : [rows[0]];
        var lines = new List<StyledLine>();

        for (var index = 0; index < shown.Length; index++)
        {
            var last = index == shown.Length - 1;
            var text = shown[index].Length == 0 && !focused ? EmptyText : shown[index];

            var shownText = focused && last ? Tail($"{text}{Caret}", valueWidth) : text;

            lines.Add(Indented(shownText, TextRole.Editable));
        }

        if (!focused && rows.Length > 1)
        {
            lines[^1] = Indented($"{shown[0]}…", TextRole.Editable);
        }

        return lines;
    }

    private static StyledLine ChosenLine(FormField field)
    {
        var labels = field.Value.Chosen.Select(id => LabelOf(field, id)).ToArray();

        return Indented(labels.Length == 0 ? EmptyText : string.Join(", ", labels), TextRole.Editable);
    }

    /// <summary>The labels as tokens wrapped to the box; the buffer being typed ends the last line, caret in view.</summary>
    private static List<StyledLine> LabelLines(FormField field, bool focused, int valueWidth)
    {
        var tokens = field.Value.Chosen.Select(label => $"[{label}]").ToList();

        if (focused)
        {
            tokens.Add(Tail($"{field.Entry}{Caret}", valueWidth));
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

    private static List<StyledLine> OptionLines(FormField field)
    {
        var visible = FormEditor.VisibleOptions(field);
        var lines = new List<StyledLine>();

        if (field.Entry.Length > 0)
        {
            lines.Add(Indented($"filter: {field.Entry}{Caret}", TextRole.Editable));
        }

        if (visible.Count == 0)
        {
            lines.Add(Indented("No match", TextRole.Muted));

            return lines;
        }

        var cursor = Math.Clamp(field.Cursor, 0, visible.Count - 1);
        var first = Math.Clamp(cursor - OptionRows + 1, 0, Math.Max(0, visible.Count - OptionRows));

        foreach (var index in Enumerable.Range(first, Math.Min(OptionRows, visible.Count - first)))
        {
            lines.Add(OptionLine(field, visible[index], index == cursor));
        }

        return lines;
    }

    private static StyledLine OptionLine(FormField field, FieldOption option, bool highlighted)
    {
        var chosen = field.Value.Chosen.Contains(option.Id) || (option.Id.Length == 0 && field.Value.Chosen.Count == 0);
        var mark = MarkOf(field.Kind, chosen);
        var pointer = highlighted ? ">" : " ";

        return Indented($"{pointer} {mark} {option.Label}", highlighted ? TextRole.Selected : TextRole.Body);
    }

    private static string MarkOf(FormFieldKind kind, bool chosen)
    {
        if (kind == FormFieldKind.MultiSelect)
        {
            return chosen ? "[x]" : "[ ]";
        }

        return chosen ? "(x)" : "( )";
    }

    private static string LabelOf(FormField field, string id)
    {
        return field.Options.FirstOrDefault(option => option.Id == id)?.Label ?? id;
    }

    private static StyledLine Indented(string text, TextRole role)
    {
        return StyledLine.Of($"{new string(' ', Indent)}{text}", role);
    }

    private sealed record FormContent(List<StyledLine> Lines, int FocusStart, int FocusLength);
}
