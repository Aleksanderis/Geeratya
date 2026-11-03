namespace Geeratya.Core.Boards;

/// <summary>One lane of a swimlane grouping.</summary>
/// <param name="Key">The stable key: an account id, an epic key, or the empty string for the leftover lane.</param>
/// <param name="Name">The name shown in the header.</param>
/// <param name="Count">The loaded issues in the lane.</param>
public sealed record SwimlaneLane(string Key, string Name, int Count);
