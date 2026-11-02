namespace Geeratya.Core.Lists;

/// <summary>The selected row and the first visible row of a list.</summary>
public readonly record struct ListPosition(int Selected, int Scroll);
