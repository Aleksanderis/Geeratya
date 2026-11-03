namespace Geeratya.Core.Views;

/// <summary>
/// The cells a tab takes in the tab bar: <paramref name="Start"/> inclusive, <paramref name="End"/> exclusive.
/// <paramref name="Close"/> is the cells of its close <c>x</c> (inside the area), null when the chip has none.
/// </summary>
public readonly record struct TabArea(TabId Tab, int Start, int End, TabButton? Close = null);
