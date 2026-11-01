using Geeratya.Core.State;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// Produces the body of the frame (everything between the header and the status bar) for the states it handles.
/// Each screen (start, help, later list, columns, detail) is one renderer, registered without editing the others.
/// </summary>
public interface IBodyRenderer
{
    /// <summary>Whether this renderer draws the body for <paramref name="state"/>.</summary>
    bool AppliesTo(AppState state);

    /// <summary>Styled lines for the body; the frame fits them to the width and height, so fewer or longer is fine.
    /// The frame pads with body-role spaces: a full-row highlight (a selected row) pads its own span to the width.</summary>
    IReadOnlyList<StyledLine> Render(AppState state, BodyContext context, int width, int height);
}
