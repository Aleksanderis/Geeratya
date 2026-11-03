namespace Geeratya.Core.Views;

/// <summary>
/// The cells a tab takes in the tab bar: <paramref name="Start"/> inclusive, <paramref name="End"/> exclusive.
/// <paramref name="Close"/> is the cells of its close <c>x</c> (inside the area), null when the chip has none.
/// <see cref="Overflow"/> marks the <c>‹n</c> and <c>+n ›</c> chips of hidden tabs (<see cref="Tab"/> is the neighbour).
/// </summary>
public readonly record struct TabArea(TabId Tab, int Start, int End, TabButton? Close = null, bool Overflow = false);
