namespace Geeratya.Core.Text;

/// <summary>The summary of a card on its two lines; the second is empty when the first holds everything.</summary>
public readonly record struct CardLines(string First, string Second);
