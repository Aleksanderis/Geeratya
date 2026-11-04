namespace Geeratya.Jira.Gateway.Models;

/// <summary>
/// Jira's "Hide done work items after" of a Kanban board, as the current user sees it (their own view setting, else
/// the published one).
/// </summary>
/// <param name="Days">The number of days; null when Jira applies no cutoff (all done issues show).</param>
public sealed record BoardViewCutoff(int? Days);
