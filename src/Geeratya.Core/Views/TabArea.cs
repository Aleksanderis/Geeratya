namespace Geeratya.Core.Views;

/// <summary>The cells a tab takes in the tab bar: <paramref name="Start"/> inclusive, <paramref name="End"/> exclusive.</summary>
public readonly record struct TabArea(TabId Tab, int Start, int End);
