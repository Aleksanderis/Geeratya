using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>One drawn line of a pick list and the row it stands for.</summary>
/// <param name="Line">The styled line.</param>
/// <param name="Item">The row index a click on the line picks, or -1 when the line is a notice or a header.</param>
/// <param name="Marker">
/// The scroll marker for the right padding column of the box row (<see cref="OverlayBox.ScrollMarker"/>): <c>▲</c>
/// or <c>▼</c> when rows are hidden above or below, else a space.
/// </param>
public sealed record PickRow(StyledLine Line, int Item, string Marker = " ");
