namespace Geeratya.Core.Panes;

/// <summary>
/// One pane's rectangle in a <see cref="PaneGrid"/>, in grid cells (not terminal cells): the layout decides which
/// panes exist and where, the pane layer only moves between them.
/// </summary>
/// <param name="Id">The pane id, unique in the grid (for example <c>description</c>).</param>
/// <param name="Column">The first grid column.</param>
/// <param name="Row">The first grid row.</param>
/// <param name="ColumnSpan">How many grid columns the pane covers, at least 1.</param>
/// <param name="RowSpan">How many grid rows the pane covers, at least 1.</param>
public sealed record PaneSlot(string Id, int Column, int Row, int ColumnSpan = 1, int RowSpan = 1)
{
    /// <summary>The column after the pane's last one.</summary>
    public int ColumnEnd => Column + Math.Max(1, ColumnSpan);

    /// <summary>The row after the pane's last one.</summary>
    public int RowEnd => Row + Math.Max(1, RowSpan);
}
