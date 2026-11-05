namespace Geeratya.Core.Detail;

/// <summary>
/// Where a line selection in the issue detail started: a content row of one pane. The row counts from the first
/// content line of the whole pane (also the part scrolled out of a Comments pane or a zoomed pane), so it stays on the
/// same text when the pane scrolls.
/// </summary>
/// <param name="Pane">The pane id (a <see cref="PaneIds"/> member).</param>
/// <param name="Row">The content row inside the pane, counted from 0.</param>
public sealed record YankAnchor(string Pane, int Row);
