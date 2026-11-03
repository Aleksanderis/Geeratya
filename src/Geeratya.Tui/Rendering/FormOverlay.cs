using System.Globalization;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;
using Geeratya.Jira.Create.Models;

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
    private const string ChooseTypeText = "Choose the issue type";
    private const string OpenAfterOnText = "on";
    private const string OpenAfterOffText = "off";
    private const string DiscardText = "Discard the changes? (y/n)";
    private const string ReadOnlyText = "not editable here";
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
    private const int ParagraphRows = 4;
    private const int PopupRows = 5;
    private const int PopupMargin = 2;
    private const string SearchingText = "searching…";
    private const string LookupFailedText = "lookup failed, showing what is loaded";
    private const string NoMatchText = "No match";
    private const string NoMoreText = "No more values";

    /// <summary>Draws the open form over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        FormState form,
        KeymapTable keymap,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
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
        var room = height - TopGap - 2;
        var footer = room >= FooterMinimumInner ? FooterHeight : 0;
        var inner = Math.Min(room, content.Lines.Count + footer);
        var box = BoxLines(form, content, keymap, boxWidth, inner, footer);
        var top = Math.Min(TopGap, height - box.Count);
        var left = (width - boxWidth) / 2;
        var result = OverlayBox.Backdrop(body);

        OverlayBox.Cover(regions, top, left, boxWidth, box.Count);

        MarkRows(regions, content, top, left, boxWidth, inner - footer);

        for (var index = 0; index < box.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], box[index], left, width);
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

        if (form.IsChoosingType)
        {
            return ChooseTypeText;
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
            lines.Add(OverlayBox.Wall(OverlayBox.Hints(FooterText(form, keymap)), contentWidth));
        }

        lines.Add(OverlayBox.Bottom(boxWidth));

        return lines;
    }

    // The visible content rows are the box rows after the top edge; each is a field row or a popup or type row.
    private static void MarkRows(
        List<OverlayRegion>? regions,
        FormContent content,
        int top,
        int left,
        int boxWidth,
        int area)
    {
        if (regions is null)
        {
            return;
        }

        var first = FirstVisible(content, Math.Max(0, area));
        var visible = Math.Max(0, Math.Min(area, content.Lines.Count - first));

        for (var index = 0; index < visible; index++)
        {
            if (content.Tags[first + index] is { } tag)
            {
                var width = Math.Max(1, boxWidth - 2);

                regions.Add(new OverlayRegion(tag.Target, top + 1 + index, left + 1, width, 1, tag.Index));
            }
        }
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
        var next = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormFieldNext);
        var previous = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormFieldPrevious);
        var submit = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormSubmit);
        var cancel = keymap.FindKeyLabel(InputMode.Insert, ActionIds.Cancel);

        if (form.DiscardPending)
        {
            var yes = keymap.FindKeyLabel(InputMode.Confirm, ActionIds.ConfirmYes);
            var no = keymap.FindKeyLabel(InputMode.Confirm, ActionIds.ConfirmNo);

            return $"{yes} discard   {no}/{cancel} keep editing";
        }

        if (form.IsChoosingType)
        {
            var down = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerNext);
            var up = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerPrevious);
            var choose = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormNext);

            return $"{down}/{up} type   {choose} choose   {cancel} cancel";
        }

        if (form.Suggest is { Source.Kind: SuggestionKind.Options, Items.Count: > 0 } choice)
        {
            var down = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerNext);
            var up = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerPrevious);
            var accept = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormNext);
            var escape = choice.Query.Length == 0 ? "cancel" : "clear filter";

            return $"{down}/{up} choose   {accept} accept   {next}/{previous} field   {cancel} {escape}";
        }

        if (form.Suggest is { Items.Count: > 0 })
        {
            var down = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerNext);
            var up = keymap.FindKeyLabel(InputMode.Insert, ActionIds.PickerPrevious);
            var accept = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormNext);

            return $"{down}/{up} choose   {next}/{accept} accept   {cancel} close list";
        }

        var browser = form.BrowserUrl is not null && form.Fields.Any(field => !field.IsEditable)
            ? $"   {keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormOpenBrowser)} browser"
            : string.Empty;
        var text = $"{next}/{previous} field   {submit} save{browser}   {cancel} cancel";

        if (form.Target is not CreateFormTarget create)
        {
            return text;
        }

        var open = keymap.FindKeyLabel(InputMode.Insert, ActionIds.FormOpenAfter);
        var state = create.OpenAfter ? OpenAfterOnText : OpenAfterOffText;

        return $"{text}   {open} open after: {state}";
    }

    private static FormContent Content(FormState form, int valueWidth)
    {
        var lines = new List<StyledLine>();
        var tags = new List<RowTag?>();
        var focusStart = 0;
        var focusLength = 0;

        AddNotices(lines, form);
        PadTags(tags, lines.Count, null);

        if (form.TypeChoices is { } types)
        {
            AddTypes(lines, form, types);

            tags.AddRange(types.Select((_, index) => new RowTag(MouseTarget.FormChoice, index)));
        }

        for (var index = 0; index < form.Fields.Count; index++)
        {
            var block = Block(form, index, valueWidth);

            if (index == form.Focus)
            {
                focusStart = lines.Count;
                focusLength = block.Count;
            }

            lines.AddRange(block);

            tags.AddRange(BlockTags(form, index, block, valueWidth));
        }

        return new FormContent(lines, focusStart, focusLength, tags);
    }

    private static void PadTags(List<RowTag?> tags, int count, RowTag? tag)
    {
        while (tags.Count < count)
        {
            tags.Add(tag);
        }
    }

    // Every row of a field block focuses the field; the rows of its open popup take the popup row instead.
    private static List<RowTag?> BlockTags(FormState form, int index, List<StyledLine> block, int valueWidth)
    {
        var tags = new List<RowTag?>();

        PadTags(tags, block.Count, new RowTag(MouseTarget.FormField, index));

        var field = form.Fields[index];
        var focused = index == form.Focus && !form.IsSaving;
        var hasPopup = field.IsEditable && field.Kind is FormFieldKind.Labels or FormFieldKind.User
            or FormFieldKind.Parent or FormFieldKind.Select or FormFieldKind.MultiSelect;

        if (!focused || !hasPopup || form.Suggest is not { } popup || popup.FieldIndex != form.Focus)
        {
            return tags;
        }

        var popupRows = PopupLines(true, form, valueWidth).Count;
        var items = popupRows - (PopupStatus(popup) is null ? 0 : 1);
        var start = block.Count - (field.Error is null ? 0 : 1) - popupRows;
        var first = PopupFirst(popup);

        for (var row = 0; row < items; row++)
        {
            tags[start + row] = new RowTag(MouseTarget.FormChoice, first + row);
        }

        return tags;
    }

    private static int PopupFirst(SuggestionState popup)
    {
        var cursor = Math.Clamp(popup.Cursor, -1, Math.Max(0, popup.Items.Count - 1));

        return Math.Clamp(Math.Max(cursor, 0) - PopupRows + 1, 0, Math.Max(0, popup.Items.Count - PopupRows));
    }

    private static void AddTypes(List<StyledLine> lines, FormState form, IReadOnlyList<CreateIssueType> types)
    {
        for (var index = 0; index < types.Count; index++)
        {
            var highlighted = index == form.TypeCursor;
            var pointer = highlighted ? ">" : " ";

            lines.Add(StyledLine.Of($"{pointer} {types[index].Name}", highlighted ? TextRole.Selected : TextRole.Body));
        }
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

        lines.AddRange(ValueLines(field, focused, form, valueWidth));

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
        var tokens = field.Value.Chosen.Select(label => $"[{LabelOf(field, label)}]").ToList();

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

    private static StyledLine Indented(string text, TextRole role)
    {
        return StyledLine.Of($"{new string(' ', Indent)}{text}", role);
    }

    private sealed record PopupNotice(string Text, TextRole Role);

    private sealed record RowTag(MouseTarget Target, int Index);

    private sealed record FormContent(List<StyledLine> Lines, int FocusStart, int FocusLength, List<RowTag?> Tags);
}
