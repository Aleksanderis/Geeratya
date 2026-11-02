using Geeratya.Core.Text;

namespace Geeratya.Core.Adf;

/// <summary>
/// The styled lines of one ADF document at one width. The result of <see cref="AdfRenderer.Render" /> depends only on
/// the document and the width, so a caller can cache it per pair and scroll over <see cref="LineCount" />.
/// </summary>
public sealed record AdfRendering(IReadOnlyList<StyledLine> Lines, int Width)
{
    /// <summary>The number of lines, for scrolling.</summary>
    public int LineCount => Lines.Count;
}
