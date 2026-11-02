namespace Geeratya.Core.Boards;

/// <summary>A card on a board: its column and its index in that column.</summary>
public readonly record struct BoardPosition(int Column, int Card);
