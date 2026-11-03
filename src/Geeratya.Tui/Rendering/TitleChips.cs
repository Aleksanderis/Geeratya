using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>The text of the chips in a top edge: the title on the left, a count or state on the right.</summary>
public sealed record TitleChips(IReadOnlyList<StyledSpan> Left, IReadOnlyList<StyledSpan>? Right);
