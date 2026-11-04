using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>One content row of a create-form pane: the line, and the mouse target it stands for (if any).</summary>
/// <param name="Line">The content, at most the pane's inner width.</param>
/// <param name="Target">What a click on the row means, or null for none.</param>
/// <param name="Index">The field index or popup row the click stands for.</param>
internal sealed record CreatePaneRow(StyledLine Line, MouseTarget? Target = null, int Index = -1);
