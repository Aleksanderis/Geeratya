namespace Geeratya.Jira.Rank.Models;

/// <summary>The outcome of ranking one issue in a multi-issue call.</summary>
/// <param name="Key">The issue key as sent.</param>
/// <param name="IsRanked">True when Jira moved this issue.</param>
/// <param name="Errors">Jira's messages when it did not; empty when moved.</param>
public sealed record RankItemResult(string Key, bool IsRanked, IReadOnlyList<string> Errors);
