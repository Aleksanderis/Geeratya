using Geeratya.Core.Mouse;
using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// A frame as styled lines: a header region, the body and the status bar line (drawn in the status style).
/// Header and body lines are exactly as wide as the frame.
/// </summary>
public sealed record StyledFrame(
    IReadOnlyList<StyledLine> Header,
    IReadOnlyList<StyledLine> Body,
    string StatusLine)
{
    /// <summary>The clickable rectangles of the open overlay in screen cells (header rows included), later on top.</summary>
    public IReadOnlyList<OverlayRegion> Regions { get; init; } = [];

    /// <summary>The cells of the <c>...</c> row menu marker in screen cells (header rows included), null when none is drawn.</summary>
    public OverlayRegion? RowMenuMarker { get; init; }

    /// <summary>The status bar as styled spans (badge chip, message in its severity role, hint chips); null draws <see cref="StatusLine"/> plain.</summary>
    public StyledLine? StatusStyled { get; init; }

    /// <summary>Where the status message sits and its severity role, null for none (the styled status bar draws it).</summary>
    public StatusSegment? StatusSegment { get; init; }

    /// <summary>Every line with the styles dropped: what <c>--print-frame</c> prints.</summary>
    public IReadOnlyList<string> PlainLines()
    {
        var lines = Header.Concat(Body).Select(line => line.PlainText).ToList();

        lines.Add(StatusLine);

        return lines;
    }
}
