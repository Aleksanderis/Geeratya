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
    private const int MaxNarrowWidth = 40;
    private const int Chrome = 4;
    private const string LoadingText = "Loading…";
    private const string SavingText = "Saving…";
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
            if (form.TopError is { } top)
            {
                return Notice(FieldRows.Error(top, inner));
            }

            return Notice(LoadingText, TextRole.Loading);
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

        if (FormFieldKinds.IsText(field.Kind))
        {
            var look = new FieldLook(inner, TextRole.Body) { Editing = true, Rows = TextRows };

            lines.AddRange(FieldRows.Value(field, look));
        }
        else if (FormFieldKinds.TakesEntry(field.Kind) || field.Entry.Length > 0)
        {
            lines.Add(FieldRows.Entry(field, inner, TextRole.Body));
        }

        var skipped = lines.Count;
        var indexes = new List<int>();

        AddRows(lines, indexes, form, field, inner);

        if (field.Error is { } error)
        {
            // A refusal is Jira's own sentence: it wraps instead of being cut, so the reason stays readable.
            var rows = FieldRows.Error(error, inner);

            lines.AddRange(rows);
            indexes.AddRange(rows.Select(static _ => -1));
        }

        return new Content(lines, skipped, indexes);
    }

    private static void AddRows(List<StyledLine> lines, List<int> indexes, FormState form, FormField field, int inner)
    {
        if (form.Suggest is not { } popup || popup.FieldIndex != form.Focus)
        {
            if (FieldRows.ShowsSearchHint(form) || FormFieldKinds.IsChoice(field.Kind))
            {
                lines.Add(StyledLine.Of(FieldRows.TypeToSearchText, TextRole.Muted));
                indexes.Add(-1);
            }

            return;
        }

        foreach (var row in PickListRenderer.Popup(popup, Math.Max(1, inner - 2)))
        {
            lines.Add(row.Line);
            indexes.Add(row.Item);
        }
    }

    private static Content Notice(string text, TextRole role)
    {
        return Notice([StyledLine.Of(text, role)]);
    }

    private static Content Notice(List<StyledLine> lines)
    {
        return new Content(lines, 0, [.. lines.Select(static _ => -1)]);
    }

    private sealed record Content(List<StyledLine> Lines, int Skipped, List<int> Indexes);
}
