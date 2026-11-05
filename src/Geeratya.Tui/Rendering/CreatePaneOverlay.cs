using Geeratya.Core.Detail;
using Geeratya.Core.Forms;
using Geeratya.Core.Keymap;
using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The create form drawn as panes in the wide overlay of the issue detail (<c>docs/ux-screens.md</c>, "Create form"):
/// a title pane with Summary, a Description pane, a Details pane (a sidebar from an overlay width of 100 columns, else
/// under the others) and a short "Not editable here" pane. The view behind is muted like behind the detail. Below 100
/// columns the overlay fills the body and is one column. The type picker and the loading state stay the small box of
/// <see cref="FormOverlay"/>. Pure.
/// </summary>
public static class CreatePaneOverlay
{
    /// <summary>The form is drawn as panes (a create form with its type chosen and its fields loaded).</summary>
    public static bool Applies(FormState form)
    {
        return CreatePanes.Applies(form);
    }

    /// <summary>Draws the form over <paramref name="body"/>, the view (<paramref name="height"/> rows).</summary>
    public static IReadOnlyList<StyledLine> Compose(
        IReadOnlyList<StyledLine> body,
        FormState form,
        KeymapTable keymap,
        bool ascii,
        int width,
        int height,
        List<OverlayRegion>? regions = null)
    {
        var box = DetailOverlayGeometry.Place(width, height);

        if (box.Width < Panel.MinFramedWidth || box.Height < 3)
        {
            return body;
        }

        var drawn = new List<OverlayRegion>();
        var lines = new CreatePaneLayout(form, keymap, ascii, drawn).Build(box.Width, box.Height);
        OverlayBox.Cover(regions, box.CardTop, box.CardLeft, box.CardWidth, box.CardHeight);

        regions?.AddRange(drawn.Select(region => region with
        {
            Top = region.Top + box.Top,
            Left = region.Left + box.Left,
        }));

        return DetailCard.Draw(body, lines, box, width);
    }
}
