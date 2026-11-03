using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>A shared edge inside a single-column frame: the content line it replaces and the chips written into it.</summary>
public sealed record PaneDivider(
    int ContentIndex,
    IReadOnlyList<StyledSpan> Left,
    IReadOnlyList<StyledSpan>? Right = null);
