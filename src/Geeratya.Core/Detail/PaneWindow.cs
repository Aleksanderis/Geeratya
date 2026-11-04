namespace Geeratya.Core.Detail;

/// <summary>The rows of a zoomed pane that are shown: the first content row and how many fit in the frame.</summary>
/// <param name="Offset">The first content row shown.</param>
/// <param name="Rows">The content rows that fit between the edges.</param>
public sealed record PaneWindow(int Offset, int Rows);
