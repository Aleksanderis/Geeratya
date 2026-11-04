namespace Geeratya.Core.Boards;

/// <summary>A card drawn in a column: its index in the column, the first row it covers and its rows.</summary>
public sealed record PlacedCard(int Card, int Top, CardGroupPart Part, IReadOnlyList<CardRow> Rows);
