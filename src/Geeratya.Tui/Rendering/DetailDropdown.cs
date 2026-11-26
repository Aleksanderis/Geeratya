using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A field form hanging under the Details field it was opened from (<c>docs/internal/ux-screens.md</c>, "Dropdowns in
/// Details"): the suggestion popup of the field form, up to five rows, the highlighted one marked with <c>&gt;</c>,
/// drawn under the value block (above it when there is no room below). The view behind is not dimmed: the detail is the
/// context of the choice. Keys and writes are the form's own. Pure.
/// </summary>
public static class DetailDropdown
{
    private const int SidebarInset = 1;
    private const string ReadOnlyText = "Not editable here";
    private const int TextRows = 4;

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
        var room = onSide ? DetailLayout.SidebarWidthFor(document.Width) - (2 * SidebarInset) : box.Width - 2;
        var wide = PickerBox.WidthFor(room);
        var left = box.Left + (onSide ? DetailLayout.MainWidth(document.Width) + DetailLayout.SidebarGap : 0)
            + SidebarInset;
        var content = ContentLines(form, wide - PickerBox.Chrome);
        var rows = content.Lines.Count;
        var boxHeight = rows + 2;
        var top = TopOf(document, detail, box, boxHeight, height);

        if (wide < 8 || top < 0 || rows == 0)
        {
            return body;
        }

        var title = form.Fields.Count == 0 ? string.Empty : Title(form);
        var lines = PickerBox.Frame(title, content.Lines, wide, content.Markers);

        var result = new List<StyledLine>(body);

        OverlayBox.Cover(regions, top, left, wide, boxHeight);
        OverlayBox.Mark(regions, MouseTarget.FormChoice, top, left, wide, 1, content.Indexes);

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
        if (form.IsLoading || form.Fields.Count == 0)
        {
            if (form.TopError is { } top)
            {
                return Notice(FieldRows.Error(top, inner));
            }

            return Notice(string.Empty, TextRole.Muted);
        }

        if (form.IsSaving)
        {
            return Notice(string.Empty, TextRole.Muted);
        }

        var field = form.Fields[Math.Clamp(form.Focus, 0, form.Fields.Count - 1)];

        if (!field.IsEditable)
        {
            return Notice(ReadOnlyText, TextRole.Muted);
        }

        if (PickerBox.Applies(form))
        {
            var picker = PickerBox.Content(form, field, inner);

            return new Content(picker.Lines, picker.Indexes, picker.Markers);
        }

        var lines = new List<StyledLine>();

        if (field.Kind == FormFieldKind.Paragraph)
        {
            var look = new FieldLook(inner, TextRole.Body) { Editing = true, Rows = TextRows };

            lines.AddRange(FieldRows.Value(field, look));
        }

        var indexes = lines.Select(static _ => -1).ToList();

        if (field.Error is { } error)
        {
            // A refusal is Jira's own sentence: it wraps instead of being cut, so the reason stays readable.
            var rows = FieldRows.Error(error, inner);

            lines.AddRange(rows);
            indexes.AddRange(rows.Select(static _ => -1));
        }

        return new Content(lines, indexes, []);
    }

    private static Content Notice(string text, TextRole role)
    {
        return Notice([StyledLine.Of(text, role)]);
    }

    private static Content Notice(List<StyledLine> lines)
    {
        return new Content(lines, [.. lines.Select(static _ => -1)], []);
    }

    private sealed record Content(List<StyledLine> Lines, List<int> Indexes, List<string> Markers);
}
