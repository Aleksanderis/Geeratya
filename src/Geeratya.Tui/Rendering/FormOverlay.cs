using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Text;
using Geeratya.Jira.Create.Models;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The field form box (<c>docs/internal/ux-screens.md</c>, "Field form overlay"): one block per field (label, value or
/// options, error) in a centred box over the body, the focused box with the focus border (<see cref="FieldBox"/>),
/// a key footer. Pure.
/// The focused block scrolls into view; a field the form cannot edit is listed read-only with the browser link.
/// </summary>
public static partial class FormOverlay
{
    private const string LoadingText = "Loading fields…";
    private const string SavingText = "Saving…";
    private const string ChooseTypeText = "Choose the issue type";
    internal const string OpenAfterOnText = "on";
    internal const string OpenAfterOffText = "off";
    private const string ReadOnlyText = "not editable here";
    private const int MaxBoxWidth = 100;
    private const int WantedBoxWidth = 90;
    private const int WantedValueWidth = 40;
    private const int ChipPadding = 8;
    private const int NarrowBoxWidth = 24;
    private const int Margin = 4;
    private const int MinimumBoxHeight = 5;
    private const int FooterHeight = 2;
    private const int FooterMinimumInner = 8;
    private const int TopGap = 1;
    private const int Indent = 4;
    private const int ParagraphRows = 4;

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

        var boxWidth = width >= NarrowBoxWidth ? Math.Min(width - Margin, WantedWidth(form)) : width;

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

    /// <summary>
    /// The box width the form wants: wide enough for the title, its longest label and a value, never from what is
    /// typed, so the box does not change width while the user types (at most the maximum width).
    /// </summary>
    private static int WantedWidth(FormState form)
    {
        var label = form.Fields.Count == 0 ? 0 : form.Fields.Max(field => TextWidth.Of(field.Label));
        var wanted = Math.Max(WantedBoxWidth, label + Indent + WantedValueWidth);

        return Math.Min(MaxBoxWidth, Math.Max(wanted, TextWidth.Of(form.Target.Title) + ChipPadding));
    }

    /// <summary>The one-line status text of the open form: the state, or the error of the focused field.</summary>
    public static string StatusText(FormState form)
    {
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

        if (focused?.Error is { } error)
        {
            return $"! {FormFieldKinds.Named(focused)}: {error}";
        }

        if (form.Pane is not null && focused is not null)
        {
            return $"{form.Target.Title} · {FormFieldKinds.Named(focused)}";
        }

        if (CreatePanes.Applies(form) && focused is not null)
        {
            return CreateStatusText(form, focused);
        }

        // Every form names the focused field after its title, whether it edits one field or several (#1030).
        if (focused is not null)
        {
            var text = $"{form.Target.Title} · {FormFieldKinds.Named(focused)}";

            return form.Navigating ? $"{text}  {FormShell.NavigateNote}" : text;
        }

        return form.Target.Title;
    }

    // Create GEN · Story · Summary, with the state of the focused field in the navigate state.
    private static string CreateStatusText(FormState form, FormField focused)
    {
        var create = (CreateFormTarget)form.Target;
        var text = $"Create {create.ProjectKey} · {create.IssueType?.Name} · {FormFieldKinds.Named(focused)}";

        return form.Navigating ? $"{text}  {FormShell.NavigateNote}" : text;
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

        var shown = content.Lines.Skip(first).Take(area).ToList();

        for (var row = 0; row < shown.Count; row++)
        {
            var marker = OverlayBox.ScrollMarker(row, area, first, content.Lines.Count);

            lines.Add(OverlayBox.Wall(shown[row], contentWidth, marker));
        }

        while (lines.Count < inner + 1 - footer)
        {
            lines.Add(OverlayBox.Wall(StyledLine.Empty, contentWidth));
        }

        if (footer > 0)
        {
            var rule = StyledLine.Of(new string(OverlayBox.Horizontal, contentWidth - 2), TextRole.Muted);

            lines.Add(OverlayBox.Wall(rule, contentWidth));
            lines.Add(OverlayBox.Wall(FormFooter.Line(form, keymap, contentWidth - 2), contentWidth));
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

        // An open picker is what the user acts on: it stays whole when it fits, else its top edge leads the view.
        return Math.Min(content.PickerStart ?? content.FocusStart, end - area);
    }

    private static FormContent Content(FormState form, int valueWidth)
    {
        var lines = new List<StyledLine>();
        var tags = new List<RowTag?>();
        var focusStart = 0;
        var focusLength = 0;
        int? pickerStart = null;

        AddNotices(lines, form, valueWidth + Indent);
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
                pickerStart = PickerTop(form, valueWidth, lines.Count, block.Count);
            }

            lines.AddRange(block);

            tags.AddRange(BlockTags(form, index, block, valueWidth));
        }

        return new FormContent(lines, focusStart, focusLength, tags) { PickerStart = pickerStart };
    }

    // The content row where the open picker of the focused block starts, or null when none is open.
    private static int? PickerTop(FormState form, int valueWidth, int blockStart, int blockLength)
    {
        if (PickerBox.Draw(form, Math.Max(6, valueWidth + Indent)) is not { } picker)
        {
            return null;
        }

        return blockStart + blockLength - picker.Lines.Count;
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

        var focused = index == form.Focus && !form.IsSaving;

        if (!focused || PickerBox.Draw(form, Math.Max(6, valueWidth + Indent)) is not { } picker)
        {
            return tags;
        }

        var start = block.Count - picker.Lines.Count;

        for (var row = 0; row < picker.Items.Count; row++)
        {
            if (picker.Items[row] >= 0)
            {
                tags[start + row] = new RowTag(MouseTarget.FormChoice, picker.Items[row]);
            }
        }

        return tags;
    }

    private static void AddTypes(List<StyledLine> lines, FormState form, IReadOnlyList<CreateIssueType> types)
    {
        for (var index = 0; index < types.Count; index++)
        {
            lines.Add(PickListRenderer.Row(types[index].Name, index == form.TypePick.Index(types.Count)));
        }
    }

    private static void AddNotices(List<StyledLine> lines, FormState form, int width)
    {
        if (form.TopError is not null)
        {
            lines.AddRange(FieldRows.Error(form.TopError, width));
        }
    }

    private sealed record RowTag(MouseTarget Target, int Index);

    private sealed record FormContent(List<StyledLine> Lines, int FocusStart, int FocusLength, List<RowTag?> Tags)
    {
        public int? PickerStart { get; init; }
    }
}
