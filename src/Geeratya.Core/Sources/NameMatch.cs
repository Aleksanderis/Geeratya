namespace Geeratya.Core.Sources;

/// <summary>The outcome of matching a typed name against a list of names.</summary>
/// <param name="Kind">No match, exactly one, or several.</param>
/// <param name="Index">The index of the unique match, -1 otherwise.</param>
/// <param name="Count">How many names matched.</param>
public readonly record struct NameMatch(NameMatchKind Kind, int Index, int Count);
