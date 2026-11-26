using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Mouse;
using Geeratya.Core.State;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The mouse rows of the Summary and Description edited in place in the issue detail: one
/// <see cref="MouseTarget.EditorText"/> region per text line the frame shows, placed from the same window the renderer
/// draws (<see cref="IssueDetailRenderer.Layout"/>), so a click or drag resolves to a line and cell of the text as in
/// the create form. Pure.
/// </summary>
internal static class DetailEditorRegions
{
    // The columns before the text of a row: the pane edge and its padding.
    private const int TextInset = 2;

    /// <summary>
    /// Adds the regions of the pane edited in place, for the detail drawn at <paramref name="box"/>, to
    /// <paramref name="regions"/> (body rows).
    /// </summary>
    public static void Add(AppState state, DetailOverlayGeometry box, List<OverlayRegion> regions)
    {
        var view = IssueDetailRenderer.Layout(state, box.Width, box.Height);
        var document = view.Document;
        var pane = view.Editing is { } editing ? document.SectionIds.ToList().IndexOf(editing) : -1;

        if (pane < 0 || state.Session.Tabs.FirstOrDefault(tab => tab.Id == state.Session.Active) is not { Form: { } form })
        {
            return;
        }

        var scroll = document.PaneScrolls[pane];
        var count = view.Editing == PaneIds.Title ? 1 : scroll?.Viewport ?? TextLines(form);
        var firstText = scroll?.Offset ?? 0;
        var width = Panel.InnerWidth(document.Side is null ? document.Width : DetailLayout.MainWidth(document.Width));

        for (var row = 0; row < count; row++)
        {
            var line = document.Sections[pane] + row;

            if (line < view.First || line >= view.First + view.Rows || line >= document.Lines.Count)
            {
                continue;
            }

            regions.Add(
                new OverlayRegion(
                    MouseTarget.EditorText,
                    box.Top + view.StripRows + line - view.First,
                    box.Left + TextInset,
                    width,
                    1,
                    form.Focus,
                    firstText + row));
        }
    }

    private static int TextLines(FormState form)
    {
        var field = form.Fields[Math.Clamp(form.Focus, 0, form.Fields.Count - 1)];

        return field.Value.Text.Count(static character => character == '\n') + 1;
    }
}
