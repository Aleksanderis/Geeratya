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
    /// <summary>Every line with the styles dropped: what <c>--print-frame</c> prints.</summary>
    public IReadOnlyList<string> PlainLines()
    {
        var lines = Header.Concat(Body).Select(line => line.PlainText).ToList();

        lines.Add(StatusLine);

        return lines;
    }
}
