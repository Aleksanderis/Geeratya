using Geeratya.Core.Text;
using Geeratya.Jira.Adf;

namespace Geeratya.Core.Adf;

/// <summary>
/// Renders an ADF document to styled terminal lines. Pure: no console access, no clock, no state. It never throws on
/// a malformed tree: unknown nodes and marks keep their text behind an <c>[unsupported: type]</c> marker, and
/// nesting beyond <see cref="AdfBlockRenderer.MaxDepth" /> is cut off with a marker. Media is never rendered: it
/// shows as <c>[image]</c> or <c>[file]</c>. Roles come from the node and mark kinds; text colour is ignored.
/// </summary>
public static class AdfRenderer
{
    /// <summary>
    /// Lays the document out for <paramref name="width" /> columns (at least 1). Lines stay within the width except
    /// where a single character is wider than the width.
    /// </summary>
    public static AdfRendering Render(AdfDocument document, int width)
    {
        var effectiveWidth = Math.Max(1, width);
        var lines = new List<StyledLine>();
        var context = new RenderContext(effectiveWidth, TextRole.Body, 0);

        AdfBlockRenderer.RenderBlocks(document.Content, context, false, lines);

        if (document.IsTruncated)
        {
            lines.Add(StyledLine.Of(AdfMarkers.Truncated, TextRole.Muted));
        }

        return new AdfRendering(lines, effectiveWidth);
    }

    /// <summary>
    /// Lays out only as much of the document as <paramref name="maxLines" /> needs: stops after the first block (or
    /// list item of a top-level list) that takes the output past it. <c>Complete</c> is false when something was left
    /// out, so the document has more than <paramref name="maxLines" /> lines; the lines returned are then the start of
    /// the full rendering. A document that crosses the limit only in its last block or item is complete, and a
    /// complete result equals <see cref="Render" />.
    /// </summary>
    public static (AdfRendering Rendering, bool Complete) RenderHead(AdfDocument document, int width, int maxLines)
    {
        var effectiveWidth = Math.Max(1, width);
        var lines = new List<StyledLine>();
        var context = new RenderContext(effectiveWidth, TextRole.Body, 0);
        var cut = AdfBlockRenderer.RenderBlocks(document.Content, context, false, lines, maxLines);

        if (!cut && document.IsTruncated)
        {
            lines.Add(StyledLine.Of(AdfMarkers.Truncated, TextRole.Muted));
        }

        return (new AdfRendering(lines, effectiveWidth), !cut);
    }
}
