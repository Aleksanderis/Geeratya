namespace Geeratya.Core.Boards;

/// <summary>One row of a card: its kind and, for summary lines and label rows, which one.</summary>
public readonly record struct CardRow(CardRowKind Kind, int Index);
