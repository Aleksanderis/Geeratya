namespace Geeratya.Core.Boards;

/// <summary>A label chip as drawn: the label (for its colour) and the text with the brackets, possibly cut.</summary>
public sealed record CardLabelChip(string Label, string Text);
