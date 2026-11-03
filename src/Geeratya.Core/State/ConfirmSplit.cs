namespace Geeratya.Core.State;

/// <summary>A confirm message cut into what happens and the warning sentences (either may be empty).</summary>
public sealed record ConfirmSplit(string Question, string Warning);
