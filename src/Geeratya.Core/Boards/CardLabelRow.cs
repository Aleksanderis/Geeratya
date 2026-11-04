namespace Geeratya.Core.Boards;

/// <summary>One row of label chips and, on the last row, how many labels did not fit (shown as <c>+N</c>).</summary>
public sealed record CardLabelRow(IReadOnlyList<CardLabelChip> Chips, int Hidden);
