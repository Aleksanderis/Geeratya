using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>
/// The two states of a title chip (<c>docs/ux-look.md</c>, section 3): the chip of the focused pane is one filled
/// accent chip with a single foreground; every other chip is dim, keeping only the colours that carry meaning (issue type,
/// status, a warning).
/// </summary>
public static class ChipStyle
{
    /// <summary>The spans as the filled accent chip: one role, whatever colour they had.</summary>
    public static IReadOnlyList<StyledSpan> Focused(IReadOnlyList<StyledSpan> spans)
    {
        return [.. spans.Select(static span => span with { Role = TextRole.ChipFocus })];
    }

    /// <summary>The spans as a dim chip: emphasis and accent roles become muted text, meaning colours stay.</summary>
    public static IReadOnlyList<StyledSpan> Dim(IReadOnlyList<StyledSpan> spans)
    {
        return [.. spans.Select(static span => span with { Role = DimRole(span.Role) })];
    }

    private static TextRole DimRole(TextRole role)
    {
        return role switch
        {
            TextRole.Heading or TextRole.Body or TextRole.Selected or TextRole.ChipFocus => TextRole.Muted,
            TextRole.Strong or TextRole.Key => TextRole.Strong,
            _ => role,
        };
    }
}
