namespace Geeratya.Core.Boards;

/// <summary>A card row under the pointer: the hit and which row of the card it is.</summary>
public readonly record struct BoardCardHit(BoardHit Hit, CardRow Row);
