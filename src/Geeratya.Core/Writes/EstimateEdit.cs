namespace Geeratya.Core.Writes;

/// <summary>An estimate changed by an edit: the value before and after (null is no estimate).</summary>
public sealed record EstimateEdit(double? Before, double? After);
