namespace Geeratya.Core.Views;

/// <summary>Which optional list columns have a value in at least one row of a tab.</summary>
public readonly record struct RowFlags(bool HasEstimate, bool HasEpic);
