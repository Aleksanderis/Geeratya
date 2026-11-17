using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>One drawn line of a pick list and the row it stands for.</summary>
/// <param name="Line">The styled line.</param>
/// <param name="Item">The row index a click on the line picks, or -1 when the line is a notice or a header.</param>
public sealed record PickRow(StyledLine Line, int Item);
