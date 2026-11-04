namespace Geeratya.Core.Detail;

/// <summary>One section of the zoom strip.</summary>
/// <param name="Id">The pane id (<see cref="PaneIds"/>).</param>
/// <param name="Label">The title text of the pane, count included (<c>Sub-tasks 3</c>).</param>
public sealed record StripSection(string Id, string Label);
