using Geeratya.Core.Text;

namespace Geeratya.Tui.Rendering;

/// <summary>What one create-form pane shows.</summary>
/// <param name="Title">The title chip.</param>
/// <param name="Right">The dim chip at the right, or null.</param>
/// <param name="Rows">The content rows (the pane scrolls when there are more than fit).</param>
/// <param name="Focused">The pane holds the focus: accent frame, filled chip.</param>
internal sealed record CreatePaneSpec(
    IReadOnlyList<StyledSpan> Title,
    IReadOnlyList<StyledSpan>? Right,
    IReadOnlyList<CreatePaneRow> Rows,
    bool Focused)
{
    /// <summary>The first row the pane must keep in view.</summary>
    public int FocusStart { get; init; }

    /// <summary>How many rows from <see cref="FocusStart"/> must be in view.</summary>
    public int FocusLength { get; init; }

    /// <summary>A row drawn at the bottom of the pane whatever the scroll (the fold hint), or null.</summary>
    public StyledLine? Pinned { get; init; }
}
