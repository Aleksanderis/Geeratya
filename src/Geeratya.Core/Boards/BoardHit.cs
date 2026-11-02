namespace Geeratya.Core.Boards;

/// <summary>A board cell under the pointer: the column, and the card in it (null on the column header).</summary>
/// <param name="Column">The column index among all board columns.</param>
/// <param name="Card">The card index in the column, null for the header.</param>
public readonly record struct BoardHit(int Column, int? Card);
