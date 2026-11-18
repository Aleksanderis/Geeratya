using Geeratya.Core.Forms;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The one look of an open picker (<c>docs/internal/ui-consistency.md</c>, component 3; owner decision 2026-11-18,
/// #1027): a box titled with the field name, an input line with the caret, then the quick rows and the search results
/// (or the search hint), then the field's error. The field's own row keeps showing its value. Every presentation of a
/// form draws an open picker through this class and decides only where the box goes: the Details dropdown overlays it
/// under the sidebar field, the create pane and the field form box insert its rows under the field. The picker kinds
/// are person, parent, priority, labels, components, sprint and custom choice or user fields
/// (<see cref="FormFieldKinds.HasPopup"/>).
/// Pure.
/// </summary>
internal static class PickerBox
{
    /// <summary>The widest a picker box gets, whatever the room.</summary>
    public const int MaxWidth = 40;

    /// <summary>Columns taken by the walls and the space inside each.</summary>
    public const int Chrome = 4;

    private const int PopupInset = 2;

    /// <summary>Whether the focused field of <paramref name="form"/> has an open picker to draw.</summary>
    public static bool Applies(FormState form)
    {
        if (form.IsSaving || form.IsLoading || form.Navigating || form.Fields.Count == 0)
        {
            return false;
        }

        var field = form.Fields[Math.Clamp(form.Focus, 0, form.Fields.Count - 1)];

        return field.IsEditable && FormFieldKinds.HasPopup(field.Kind);
    }

    /// <summary>The box width for <paramref name="available"/> columns: the room, at most <see cref="MaxWidth"/>.
    /// </summary>
    public static int WidthFor(int available)
    {
        return Math.Min(MaxWidth, available);
    }

    /// <summary>
    /// The box of the open picker for <paramref name="available"/> columns of room, or null when the focused field
    /// has none. <see cref="PickerLines.Items"/> has one entry per line: the suggestion a click on the line takes, or
    /// -1 for the edges, the input line, the hint and the error.
    /// </summary>
    public static PickerLines? Draw(FormState form, int available)
    {
        if (!Applies(form))
        {
            return null;
        }

        var width = WidthFor(available);
        var field = form.Fields[Math.Clamp(form.Focus, 0, form.Fields.Count - 1)];
        var content = Content(form, field, Math.Max(1, width - Chrome));
        var items = new List<int> { -1 };

        items.AddRange(content.Indexes);
        items.Add(-1);

        return new PickerLines(Frame(field.Label, content.Lines, width, content.Markers), items);
    }

    /// <summary>
    /// The box around <paramref name="content"/>: the title in the top edge, the walls (the right padding column of a
    /// row carries its entry of <paramref name="markers"/>, the scroll marker of a cut list), the bottom edge.
    /// </summary>
    public static List<StyledLine> Frame(
        string title,
        IReadOnlyList<StyledLine> content,
        int width,
        IReadOnlyList<string>? markers = null)
    {
        var lines = new List<StyledLine> { OverlayBox.Top(width, title) };

        for (var row = 0; row < content.Count; row++)
        {
            var marker = markers is not null && row < markers.Count ? markers[row] : " ";

            lines.Add(OverlayBox.Wall(content[row], width - 2, marker));
        }

        lines.Add(OverlayBox.Bottom(width));

        return lines;
    }

    /// <summary>
    /// The rows inside the box of a picker field, <paramref name="inner"/> columns wide: the input line, the rows of
    /// the open list (or the search hint) and the error. Item indexes match the lines (-1 for no click).
    /// </summary>
    public static PickerContent Content(FormState form, FormField field, int inner)
    {
        var lines = new List<StyledLine> { FieldRows.Entry(field, inner) };
        var indexes = new List<int> { -1 };
        var markers = new List<string> { " " };

        if (form.Suggest is { } popup && popup.FieldIndex == form.Focus)
        {
            foreach (var row in PickListRenderer.Popup(popup, Math.Max(1, inner - PopupInset)))
            {
                lines.Add(row.Line);
                indexes.Add(row.Item);
                markers.Add(row.Marker);
            }
        }
        else if (FieldRows.ShowsSearchHint(form) || FormFieldKinds.IsChoice(field.Kind))
        {
            lines.Add(StyledLine.Of(FieldRows.TypeToSearchText, TextRole.Muted));
            indexes.Add(-1);
            markers.Add(" ");
        }

        if (field.Error is { } error)
        {
            // A refusal is Jira's own sentence: it wraps instead of being cut, so the reason stays readable.
            var rows = FieldRows.Error(error, inner);

            lines.AddRange(rows);
            indexes.AddRange(rows.Select(static _ => -1));
            markers.AddRange(rows.Select(static _ => " "));
        }

        return new PickerContent(lines, indexes, markers);
    }
}

/// <summary>The rows inside a picker box, the suggestion each stands for (-1 for none) and its scroll marker.</summary>
internal sealed record PickerContent(List<StyledLine> Lines, List<int> Indexes, List<string> Markers);

/// <summary>The whole box of a picker (edges included) and the suggestion each line stands for (-1 for none).</summary>
internal sealed record PickerLines(List<StyledLine> Lines, List<int> Items);
