using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Suggestions;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A field form hanging under the Details field it was opened from (<c>docs/ux-screens.md</c>, "Dropdowns in
/// Details"): the suggestion popup of the field form, up to five rows, the highlighted one marked with <c>&gt;</c>,
/// drawn under the value block (above it when there is no room below). The view behind is not dimmed: the detail is the
/// context of the choice. Keys and writes are the form's own. Pure.
/// </summary>
public static class DetailDropdown
{
    private const int PopupRows = 5;
    private const int SidebarInset = 1;
    private const int MaxNarrowWidth = 40;
    private const int Chrome = 4;
    private const string LoadingText = "Loading…";
    private const string SavingText = "Saving…";
    private const string ReadOnlyText = "Not editable here";
    private const string TypeText = "Type to search";
    private const string Caret = "_";

    /// <summary>Whether <paramref name="form"/> is drawn as a dropdown over the open detail.</summary>
    public static bool Applies(AppState state, FormState form)
    {
        return form.Dropdown is not null && Open(state) is { Document: not null };
    }

    /// <summary>Draws the dropdown over <paramref name="body"/> (exactly <paramref name="height"/> lines).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        AppState state,
        FormState form,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        if (Open(state) is not { Document: { } document } detail)
        {
            return body;
        }

        var box = DetailOverlayGeometry.Place(width, height);
        var onSide = document.Side is not null && detail.SideCursor is not null;
        var wide = onSide ? DetailLayout.SidebarWidthFor(document.Width) - (2 * SidebarInset) : Math.Min(MaxNarrowWidth, box.Width - 2);
        var left = box.Left + (onSide ? DetailLayout.MainWidth(document.Width) + DetailLayout.SidebarGap : 0)
            + SidebarInset;
        var content = ContentLines(form, wide - Chrome);
        var rows = content.Lines.Count;
        var boxHeight = rows + 2;
        var top = TopOf(document, detail, box, boxHeight, height);

        if (wide < 8 || top < 0 || rows == 0)
        {
            return body;
        }

        var lines = new List<StyledLine> { OverlayBox.Top(wide, form.Fields.Count == 0 ? string.Empty : Title(form)) };

        lines.AddRange(content.Lines.Select(line => OverlayBox.Wall(line, wide - 2)));
        lines.Add(OverlayBox.Bottom(wide));

        var result = new List<StyledLine>(body);

        OverlayBox.Cover(regions, top, left, wide, boxHeight);
        OverlayBox.Mark(regions, MouseTarget.FormChoice, top, left, wide, 1 + content.Skipped, content.Indexes);

        for (var index = 0; index < lines.Count && top + index < result.Count; index++)
        {
            result[top + index] = OverlayBox.Over(result[top + index], lines[index], left, width);
        }

        return result;
    }

    private static DetailState? Open(AppState state)
    {
        var tab = state.Session.Active is { } id ? state.Session.Tabs.FirstOrDefault(tab => tab.Id == id) : null;

        return tab is { Detail.Count: > 0 } ? tab.Detail[^1] : null;
    }

    private static string Title(FormState form)
    {
        return form.Fields[Math.Clamp(form.Focus, 0, form.Fields.Count - 1)].Label;
    }

    // The body row of the popup's top edge: under the last value row of the cursor field, above its label when the
    // overlay has no room below; -1 when the field is not on screen.
    private static int TopOf(
        DetailDocument document,
        DetailState detail,
        DetailOverlayGeometry box,
        int rows,
        int height)
    {
        var onSide = document.Side is not null && detail.SideCursor is not null;
        var shown = onSide ? document.Side! : document;
        var cursor = onSide ? detail.SideCursor!.Value : detail.Cursor;
        var strip = detail.Zoomed is null || onSide ? 0 : DetailZoom.StripRows;
        var offset = onSide ? SideView.FirstLine(shown, cursor, box.Height) : detail.Scroll - strip;
        var block = shown.BlockAt(cursor) ?? new LineBlock(cursor, cursor + 1);
        var under = box.Top + block.End - offset;
        var above = box.Top + block.Start - offset - rows;

        if (block.End - offset > box.Height || block.Start - offset < strip)
        {
            return -1;
        }

        if (under + rows <= box.Top + box.Height || above < box.Top)
        {
            return Math.Min(under, Math.Max(0, height - rows));
        }

        return above;
    }

    private static Content ContentLines(FormState form, int inner)
    {
        var lines = new List<StyledLine>();

        if (form.IsLoading || form.Fields.Count == 0)
        {
            return Notice(form.TopError ?? LoadingText, form.TopError is null ? TextRole.Loading : TextRole.Error);
        }

        if (form.IsSaving)
        {
            return Notice(SavingText, TextRole.Loading);
        }

        var field = form.Fields[Math.Clamp(form.Focus, 0, form.Fields.Count - 1)];

        if (!field.IsEditable)
        {
            return Notice(ReadOnlyText, TextRole.Muted);
        }

        var typed = field.Kind is FormFieldKind.Labels or FormFieldKind.User or FormFieldKind.Parent;

        if (typed || field.Entry.Length > 0)
        {
            lines.Add(StyledLine.Of(TextWidth.Fit($"{field.Entry}{Caret}", inner)));
        }

        var skipped = lines.Count;
        var indexes = new List<int>();

        AddRows(lines, indexes, form, inner);

        if (field.Error is { } error)
        {
            lines.Add(StyledLine.Of(TextWidth.Fit(error, inner), TextRole.Error));
            indexes.Add(-1);
        }

        return new Content(lines, skipped, indexes);
    }

    private static void AddRows(List<StyledLine> lines, List<int> indexes, FormState form, int inner)
    {
        if (form.Suggest is not { } popup || popup.FieldIndex != form.Focus)
        {
            lines.Add(StyledLine.Of(TypeText, TextRole.Muted));
            indexes.Add(-1);

            return;
        }

        var cursor = Math.Clamp(popup.Cursor, -1, Math.Max(0, popup.Items.Count - 1));
        var first = FormOverlay.PopupFirst(popup);
        var shown = Math.Min(PopupRows, Math.Max(0, popup.Items.Count - first));

        foreach (var index in Enumerable.Range(first, shown))
        {
            var text = TextWidth.Fit(SuggestionText.Row(popup.Items[index]), Math.Max(1, inner - 2));
            var highlighted = index == cursor;
            var pointer = highlighted ? ">" : " ";

            lines.Add(StyledLine.Of($"{pointer} {text}", highlighted ? TextRole.Selected : TextRole.Body));
            indexes.Add(index);
        }

        if (FormOverlay.PopupStatus(popup) is { } status)
        {
            lines.Add(StyledLine.Of(TextWidth.Fit(status.Text, inner), status.Role));
            indexes.Add(-1);
        }
    }

    private static Content Notice(string text, TextRole role)
    {
        return new Content([StyledLine.Of(text, role)], 0, [-1]);
    }

    private sealed record Content(List<StyledLine> Lines, int Skipped, List<int> Indexes);
}
